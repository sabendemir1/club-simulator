(() => {
  // ===== Modes =====
  // 'product' highlights whole product cards; 'minimal' highlights whatever is under the cursor.
  let PHIA_MODE = 'product'; // press Alt+M to toggle

  // ===== Your existing minimal highlighter state (kept) =====
  let last;
  const HIGHLIGHT = '3px solid #ffd400';

  function clear() {
    if (last) {
      last.style.outline = last.__oldOutline || '';
      delete last.__oldOutline;
      last = null;
    }
  }

  // ===== Product detection helpers =====
  const CANDIDATE_SELECTORS = [
    // Common cards
    '.product', '.product-card', '.product-item', '.product-tile', '.grid-product',
    // Amazon
    '.s-result-item', '[data-asin]',
    // Shopify / Woo
    '.product-grid-item', '.grid__item', '.products .product',
    // Schema.org
    '[itemtype*="Product"]', '[itemscope][itemtype*="Product"]'
  ];

  const PRICE_HINTS = ['price','sale','amount','offer'];
  const NAME_HINTS  = ['title','name'];

  const hasClassLike = (el, hints) => {
    const c = (el.className || '').toString().toLowerCase();
    return hints.some(h => c.includes(h));
  };

  const findPriceInside = (root) => {
    if (!root) return null;
    const micro = root.querySelector('[itemprop="price"], [data-price]');
    if (micro) return micro;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT);
    let node; const moneyRe = /(\$|€|£|¥|\d+[\.,]\d{2})/;
    while ((node = walker.nextNode())) {
      if (hasClassLike(node, PRICE_HINTS) && moneyRe.test(node.textContent)) return node;
    }
    return null;
  };

  const findNameInside = (root) => {
    if (!root) return null;
    let n = root.querySelector('h1, h2, h3, [itemprop="name"], .product-title, .title');
    if (n && n.textContent.trim().length > 0) return n;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT);
    let node;
    while ((node = walker.nextNode())) {
      if (hasClassLike(node, NAME_HINTS) && node.textContent.trim().length > 2) return node;
    }
    return null;
  };

  // Find the whole product card near the cursor
  const getProductContainer = (start) => {
    if (!start || !(start instanceof Element)) return null;
    const direct = start.closest(CANDIDATE_SELECTORS.join(','));
    if (direct) return direct;
    // climb up a bit and require both a name and a price inside
    let el = start;
    for (let i = 0; i < 8 && el && el !== document.body; i++) {
      if (findNameInside(el) && findPriceInside(el)) return el;
      el = el.parentElement;
    }
    return null;
  };

  // ===== Hover handler (now with modes) =====
  let rafPending = false;
  document.addEventListener('mousemove', (e) => {
    if (rafPending) return;
    rafPending = true;
    requestAnimationFrame(() => {
      rafPending = false;

      const candidate = (PHIA_MODE === 'minimal')
        ? (e.target instanceof Element ? e.target.closest('*') : null)
        : getProductContainer(e.target);

      if (!candidate || candidate === last) return;

      // clear old
      clear();

      // set new
      last = candidate;
      last.__oldOutline = last.style.outline;
      last.style.outline = HIGHLIGHT;
      // keep outlines inside the card (optional)
      last.style.outlineOffset = '-3px';
      last.style.borderRadius = '8px';
    });
  }, { passive: true });

  document.addEventListener('mouseleave', clear, { passive: true });

  // ===== Keyboard toggles =====
  // Alt+M: switch between 'product' and 'minimal'
  // Alt+H: quickly hide/show highlighting without unloading the extension
  let enabled = true;
  window.addEventListener('keydown', (e) => {
    if (e.altKey && (e.key === 'm' || e.key === 'M')) {
      PHIA_MODE = (PHIA_MODE === 'product') ? 'minimal' : 'product';
      clear();
      console.info('[Phia] Mode:', PHIA_MODE.toUpperCase());
    }
    if (e.altKey && (e.key === 'h' || e.key === 'H')) {
      enabled = !enabled;
      if (!enabled) clear();
      console.info('[Phia] Highlighter', enabled ? 'ENABLED' : 'DISABLED');
    }
  }, { passive: true });

  // respect enabled flag
  const origAddEventListener = document.addEventListener.bind(document);
  document.addEventListener = function(type, listener, opts) {
    if (type === 'mousemove') {
      const wrapped = (evt) => { if (enabled) listener(evt); };
      return origAddEventListener(type, wrapped, opts);
    }
    return origAddEventListener(type, listener, opts);
  };

  console.info('[Phia] Highlighter loaded. Mode=PRODUCT (Alt+M to toggle, Alt+H to pause)');
  // --- helpers ---
const clean = (s) => (s || "").replace(/\s+/g, " ").trim();

function getVisibleText(root) {
  // innerText already skips most hidden stuff; trim & collapse whitespace
  return clean(root.innerText);
}

function getStructuredFields(root) {
  // try common fields first
  const titleEl =
    root.querySelector('h1, h2, h3, .product-title, .title, [itemprop="name"]');
  const priceEl =
    root.querySelector('[itemprop="price"], .price, .sale-price, [data-price]');
  const skuEl = root.querySelector('[itemprop="sku"], .sku, [data-sku]');
  const brandEl =
    root.querySelector('[itemprop="brand"], .brand, [data-brand]');

  return {
    title: clean(titleEl?.textContent) || "",
    price: clean(priceEl?.textContent) || "",
    sku: clean(skuEl?.textContent || skuEl?.getAttribute?.("content")) || "",
    brand: clean(
      brandEl?.textContent ||
      brandEl?.getAttribute?.("content") ||
      brandEl?.querySelector?.('[itemprop="name"]')?.textContent
    ) || ""
  };
}

function getJSONLDProduct() {
  // optional: parse page-level JSON-LD Product
  for (const s of document.querySelectorAll('script[type="application/ld+json"]')) {
    try {
      const data = JSON.parse(s.textContent);

      const items = Array.isArray(data) ? data : [data];
      for (const it of items) {
        const types = Array.isArray(it["@type"]) ? it["@type"] : [it["@type"]];
        if (types?.includes?.("Product") || it["@type"] === "Product") {
          return {
            title: clean(it.name),
            price: clean(it.offers?.price),
            currency: clean(it.offers?.priceCurrency),
            sku: clean(it.sku),
            brand: clean(it.brand?.name || it.brand)
          };
        }
      }
    } catch {}
  }
  return null;
}

// --- UI: small toast to show what we captured ---
function toast(msg) {
  const t = document.createElement("div");
  t.textContent = msg;
  t.style.cssText =
    "position:fixed;right:12px;bottom:12px;padding:8px 10px;background:#111;color:#fff;border-radius:8px;z-index:2147483647;font:12px system-ui";
  document.body.appendChild(t);
  setTimeout(() => t.remove(), 1600);
}

function showResultsPanel(results, usedQuery) {
  let p = document.getElementById("phia-results");
  if (!p) {
    p = document.createElement("div");
    p.id = "phia-results";
    p.style.cssText = [
      "position:fixed;right:12px;bottom:12px;max-width:520px;max-height:60vh;overflow:auto;",
      "background:#fff;border:2px solid #ffd400;border-radius:12px;",
      "box-shadow:0 10px 30px rgba(0,0,0,.2);padding:10px;z-index:2147483647;",
      "font:13px system-ui"
    ].join("");
    document.body.appendChild(p);
  }

  const items = (results || []).map(r => `
    <div style="margin:8px 0;padding:8px;border:1px solid #eee;border-radius:8px">
      <a href="${r.url}" target="_blank" style="font-weight:600; text-decoration:none;">
        ${r.title || r.url}
      </a>
      <div style="color:#666; font-size:12px;">${new URL(r.url).hostname}</div>
    </div>
  `).join("");

  p.innerHTML = `
    <div style="margin-bottom:6px;font-weight:700">
      Top results for: ${usedQuery || "(query)"}
      <button id="phia-close" style="float:right;border:0;background:#ffd400;padding:4px 8px;border-radius:6px;cursor:pointer">Close</button>
    </div>
    ${items || "<div style='color:#666'>No results</div>"}
  `;

  document.getElementById("phia-close")?.addEventListener("click", () => p.remove(), { once:true });
}

function buildQuery({ text, fields, jsonld }) {
  const sku = (fields?.sku || jsonld?.sku || "").trim();
  if (sku) return sku;
  // strip sizes / promos / prices
  let t = (fields?.title || jsonld?.title || text || "");
  t = t.replace(/\b(XXXL|XXL|XL|L|M|S|XS|2XL|3XL|4XL|5XL)\b/gi, "")
       .replace(/\b(indirim|discount|promo|kişiselleştir|bought|reviews?|stars?)\b/gi, "")
       .replace(/(\d[\d.,]*\s?(tl|€|\$|£|usd|eur|gbp))/gi, "")
       .replace(/\s+/g, " ")
       .trim();
  return t.split(" ").slice(0, 12).join(" ");
}

window.addEventListener("click", (e) => {
  if (!(e.ctrlKey && e.shiftKey)) return;   // hold Ctrl+Shift and click the highlighted element
  const targetEl = last instanceof Element ? last : null;
  if (!targetEl) return;

  e.preventDefault();
  e.stopPropagation();

  const text = getVisibleText(targetEl);
  const fields = getStructuredFields(targetEl);
  const jsonld = getJSONLDProduct();

  const payload = {
    url: location.href,
    merchant: location.hostname,
    mode: typeof PHIA_MODE !== "undefined" ? PHIA_MODE : "unknown",
    rawText: text,
    fields,
    jsonld
  };

  console.info("[Phia] scraped:", payload);
  toast("✅ scraped text & fields (see console)");
  
  const q = buildQuery({ text, fields, jsonld });
  chrome.runtime.sendMessage({ type: "SEARCH", query: q, limit: 10 }, (resp) => {
  if (chrome.runtime.lastError) return console.warn(chrome.runtime.lastError.message);
  if (resp?.ok) showResultsPanel(resp.results || [], resp.usedQuery ?? q);
});

  // --- download payload as result.json ---
  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "result.json";
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);

  // optional: copy to clipboard
  // navigator.clipboard?.writeText(text).catch(()=>{});
  // optional: send to backend
  // fetch("http://localhost:8787/match", { method:"POST", headers:{ "Content-Type":"application/json" }, body: JSON.stringify(payload) });
}, true);
})();