// background.js — local in-browser search (no server)

// --- simple normalizer and scorer (title/brand/sku overlap) ---
const norm = s => (s || "").toLowerCase().replace(/\s+/g, " ").trim();

function scoreOffer(q, o) {
  const t = norm(o.title), b = norm(o.brand || ""), sku = norm(o.sku || "");
  let s = 0;
  if (t.includes(q)) s += 5;
  if (b && q.includes(b)) s += 2;
  if (sku && q.includes(sku)) s += 3;

  // token overlap bonus
  const qTok = new Set(q.split(" "));
  const tTok = new Set(t.split(" "));
  let overlap = 0; qTok.forEach(w => { if (tTok.has(w)) overlap++; });
  s += Math.min(3, overlap);

  return s;
}

async function searchLocal(query, limit = 10) {
  const q = norm(query);
  const { offers = [] } = await chrome.storage.local.get("offers");
  if (!q) return offers.slice(0, limit);
  return offers
    .map(o => ({ o, s: scoreOffer(q, o) }))
    .filter(x => x.s > 0)
    .sort((a,b) => b.s - a.s)
    .slice(0, limit)
    .map(x => x.o);
}

async function webSearch(query, limit = 10) {
  return new Promise((resolve, reject) => {
    chrome.tabs.create({ url: "https://duckduckgo.com/?q=" + encodeURIComponent(query), active: false }, (tab) => {
      // Wait for the tab to load
      chrome.tabs.onUpdated.addListener(function listener(tabId, info) {
        if (tabId === tab.id && info.status === "complete") {
          chrome.tabs.onUpdated.removeListener(listener);
          chrome.scripting.executeScript({
            target: { tabId: tab.id },
            func: () => {
              return [...document.querySelectorAll(".result__a")].map(a => ({
                title: a.textContent,
                url: a.href
              }));
            }
          }, (results) => {
            //chrome.tabs.remove(tab.id);
            if (chrome.runtime.lastError || !results || !results[0]) {
              resolve([]);
            } else {
              resolve(results[0].result.slice(0, limit));
            }
          });
        }
      });
    });
  });
}

// API: LOAD_OFFERS, CLEAR_OFFERS, SEARCH
chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  (async () => {
    if (msg?.type === "SEARCH") {
      const results = await webSearch(msg.query, msg.limit);
      sendResponse({ ok: true, results });
      console.log("hello");
    } else {    
      sendResponse({ ok: false, error: "unknown_type" });
    }
  })();
  return true; // keep the port open for async
});