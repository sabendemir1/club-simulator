using System;
using System.Collections;
using System.Collections.Generic;

namespace StripSim_Door_Alternative1
{
    public class ClientExitTimeLinkedList : IEnumerable<Client>
    {
        private class Node
        {
            public Client Value;
            public Node Next;
            public Node(Client value) { Value = value; }
        }

        private Node head;
        private int count;

        public int Count => count;

        // Insert client sorted by exitTime (ascending)
        public void Insert(Client client)
        {
            var newNode = new Node(client);

            if (head == null || client.exitTime < head.Value.exitTime)
            {
                newNode.Next = head;
                head = newNode;
            }
            else
            {
                Node current = head;
                while (current.Next != null && current.Next.Value.exitTime <= client.exitTime)
                {
                    current = current.Next;
                }
                newNode.Next = current.Next;
                current.Next = newNode;
            }
            count++;
        }

        // Remove and return the first client (with earliest exitTime)
        public Client RemoveFirst()
        {
            if (head == null) return null;
            var value = head.Value;
            head = head.Next;
            count--;
            return value;
        }

        // Peek at the first client (with earliest exitTime)
        public Client PeekFirst()
        {
            return head?.Value;
        }

        public IEnumerator<Client> GetEnumerator()
        {
            Node current = head;
            while (current != null)
            {
                yield return current.Value;
                current = current.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
