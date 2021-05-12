using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityPIC
{
    namespace Utils
    {
        public class PriorityQueue<DataType> : PriorityQueue<DataType, int> { }

        public class PriorityQueue<DataType, PriorityType> : IEnumerable<DataType>
        {
            public struct InternalDataType
            {
                public DataType Data;
                public PriorityType Priority;
                public int HeapIndex;
            }

            public struct Ref
            {
                public bool IsValid { get => Owner != null; }
                public readonly int Index;

                public static Ref Null = new Ref(null, -1);
                public readonly PriorityQueue<DataType, PriorityType> Owner;

                public Ref(PriorityQueue<DataType, PriorityType> owner, int dataIndex)
                {
                    Owner = owner;
                    Index = dataIndex;
                }
            }

            private int _count;
            private int _capacity;
            private InternalDataType[] _data;
            private int[] _heap;
            private IComparer<PriorityType> _cmp;
            private Stack<int> _freeSlots;

            public int Count { get { return _count; } }
            public int Capacity { get { return _capacity; } }

            public PriorityQueue(int capacity=1)
            {
                _count = 0;
                _capacity = capacity;
                _data = new InternalDataType[capacity];
                _heap = new int[capacity];
                _cmp = Comparer<PriorityType>.Default;
                _freeSlots = new Stack<int>();
            }

            public PriorityQueue(IComparer<PriorityType> cmp, int capacity=1)
            {
                _count = 0;
                _capacity = capacity;
                _data = new InternalDataType[capacity];
                _heap = new int[capacity];
                _cmp = cmp;
                _freeSlots = new Stack<int>();
            }

            public DataType Peek()
            {
                return _data[_heap[0]].Data;
            }

            public Ref Enqueue(DataType d, PriorityType p)
            {
                if (_count == _capacity)
                {
                    _capacity <<= 1;
                    InternalDataType[] newData = new InternalDataType[_capacity];
                    int[] newHeap = new int[_capacity];
                    _data.CopyTo(newData, 0);
                    _data = newData;
                    _heap.CopyTo(newHeap, 0);
                    _heap = newHeap;
                }

                int index;

                if (_freeSlots.Count > 0)
                {
                    index = _freeSlots.Pop();
                }
                else
                {
                    index = _count;
                }

                _data[index].Data = d;
                _data[index].Priority = p;
                _data[index].HeapIndex = _count;
                _heap[_count] = index;

                heapUp(_count++);

                Ref r = new Ref(this, index);

                return r;
            }

            public DataType Dequeue()
            {
#if UNITYPIC_DEBUG
                if (_count == 0)
                {
                    throw new System.Exception();
                }
#endif

                --_count;

                int headIndex = _heap[0];
                DataType data = _data[headIndex].Data;

                _data[headIndex].Data = default;
                _data[headIndex].Priority = default;

                if (_count > 0)
                {
                    _freeSlots.Push(headIndex);

                    _heap[0] = _heap[_count];
                    _data[_heap[0]].HeapIndex = 0;
                    heapDown(0);
                }
                else
                {
                    _freeSlots.Clear();
                }

                return data;
            }

            public void Remove(ref Ref r)
            {
#if UNITYPIC_DEBUG
                if (r.Owner != this || _count == 0)
                {
                    throw new System.Exception();
                }
#endif

                --_count;

                int heapIndex = _data[r.Index].HeapIndex;

                _data[r.Index].Data = default;
                _data[r.Index].Priority = default;

                if (_count > 0)
                {
                    _freeSlots.Push(r.Index);

                    if (heapIndex != _count)
                    {
                        int dataIndex = _heap[_count];

                        _heap[heapIndex] = dataIndex;
                        _data[dataIndex].HeapIndex = heapIndex;

                        heapUp(_data[dataIndex].HeapIndex);
                        heapDown(_data[dataIndex].HeapIndex);
                    }
                }
                else
                {
                    _freeSlots.Clear();
                }

                r = Ref.Null;
            }

            public void UpdatePriority(ref Ref r, PriorityType p)
            {
#if UNITYPIC_DEBUG
                if (r.Owner != this)
                {
                    throw new System.Exception();
                }
#endif
                _data[r.Index].Priority = p;
                heapUp(_data[r.Index].HeapIndex);
                heapDown(_data[r.Index].HeapIndex);
            }

            public void Clear()
            {
                while (_count > 0)
                {
                    _data[_heap[--_count]].Data = default;
                }

                _freeSlots.Clear();
            }

            public IEnumerator<DataType> GetEnumerator()
            {
                for (int i = 0; i < _count; ++i)
                {
                    yield return _data[_heap[i]].Data;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private int parent(int i) { return (i - 1) >> 1; }
            private int left(int i) { return (i << 1) + 1; }
            private int right(int i) { return (i << 1) + 2; }

            private void heapUp(int i)
            {
                int p;
                PriorityType ip = _data[_heap[i]].Priority;

                while (i > 0)
                {
                    p = (i - 1) >> 1;

                    if (_cmp.Compare(_data[_heap[p]].Priority, ip) >= 0) break;

                    int temp = _heap[i];
                    _heap[i] = _heap[p];
                    _heap[p] = temp;

                    _data[_heap[i]].HeapIndex = i;
                    _data[_heap[p]].HeapIndex = p;

                    i = p;
                }
            }

            private void heapDown(int i)
            {
                int l, r, t;
                PriorityType lp, rp, tp, ip;

                ip = _data[_heap[i]].Priority;

                while (true)
                {
                    l = (i << 1) + 1;

                    if (l >= _count) break;

                    r = l + 1;
                    lp = _data[_heap[l]].Priority;

                    if (r >= _count)
                    {
                        t = l;
                        tp = lp;
                    }
                    else
                    {
                        rp = _data[_heap[r]].Priority;

                        if (_cmp.Compare(lp, rp) > 0)
                        {
                            t = l;
                            tp = lp;
                        }
                        else
                        {
                            t = r;
                            tp = rp;
                        }
                    }

                    if (_cmp.Compare(ip, _data[_heap[t]].Priority) < 0)
                    {
                        int temp = _heap[i];
                        _heap[i] = _heap[t];
                        _heap[t] = temp;

                        _data[_heap[i]].HeapIndex = i;
                        _data[_heap[t]].HeapIndex = t;
                    }
                    else
                    {
                        break;
                    }

                    i = t;
                }
            }

#if UNITYPIC_DEBUG
            private void healthCheck()
            {
                HashSet<int> seen = new HashSet<int>();
                for (int i = 0; i < _count; ++i)
                {
                    if (seen.Contains(_heap[i]))
                    {
                        throw new System.Exception();
                    }

                    seen.Add(_heap[i]);
                }

                for (int i = 1; i < _count; ++i)
                {
                    if (_cmp.Compare(_data[_heap[i]].Priority, _data[_heap[parent(i)]].Priority) > 0)
                    {
                        throw new System.Exception("Bad pq");
                    }
                }
            }
#endif
        }
    }
}

