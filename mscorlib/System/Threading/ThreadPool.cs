// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
//
// <OWNER>[....]</OWNER> 
/*============================================================================== 
**
** Class: ThreadPool 
**
**
** Purpose: Class for creating and managing a threadpool
** 
**
=============================================================================*/ 
 
#pragma warning disable 0420
 
/*
 * Below you'll notice two sets of APIs that are separated by the
 * use of 'Unsafe' in their names.  The unsafe versions are called
 * that because they do not propagate the calling stack onto the 
 * worker thread.  This allows code to lose the calling stack and
 * thereby elevate its security privileges.  Note that this operation 
 * is much akin to the combined ability to control security policy 
 * and control security evidence.  With these privileges, a person
 * can gain the right to load assemblies that are fully trusted which 
 * then assert full trust and can call any code they want regardless
 * of the previous stack information.
 */
 
namespace System.Threading
{ 
    using System.Security; 
    using System.Runtime.Remoting;
    using System.Security.Permissions; 
    using System;
    using Microsoft.Win32;
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution; 
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning; 
    using System.Collections.Generic; 
    using System.Diagnostics.Contracts;
 
    internal static class ThreadPoolGlobals
    {
#if FEATURE_LEGACY_THREADPOOL
        public static bool useNewWorkerPool = ThreadPool.ShouldUseNewWorkerPool(); 
#endif
 
        //Per-appDomain quantum (in ms) for which the thread keeps processing 
        //requests in the current domain.
 
#if FEATURE_LEGACY_THREADPOOL
        public static uint tpQuantum = ThreadPool.ShouldUseNewWorkerPool() ? 30U : 2U;
#else
        public static uint tpQuantum = 30U; 
#endif
 
        public static int processorCount = Environment.ProcessorCount; 

#if FEATURE_LEGACY_THREADPOOL 
        public static int tpWarmupCount =  Environment.ProcessorCount * 2;
#endif

        public static bool tpHosted = ThreadPool.IsThreadPoolHosted(); 

        public static bool vmTpInitialized; 
        public static bool enableWorkerTracking; 

#if FEATURE_LEGACY_THREADPOOL 
        public static ThreadPoolRequestQueue tpQueue = ThreadPool.ShouldUseNewWorkerPool() ? null : new ThreadPoolRequestQueue();

        [SecurityCritical]
        public static ThreadPoolWorkQueue workQueue = ThreadPool.ShouldUseNewWorkerPool() ? new ThreadPoolWorkQueue() : null; 
#else
        [SecurityCritical] 
        public static ThreadPoolWorkQueue workQueue = new ThreadPoolWorkQueue(); 
#endif
 
        [System.Security.SecuritySafeCritical] // static constructors should be safe to call
        static ThreadPoolGlobals()
        {
        } 
    }
 
#if FEATURE_LEGACY_THREADPOOL 
    internal sealed class ThreadPoolRequestQueue
    { 
        private _ThreadPoolWaitCallback tpHead;
        private _ThreadPoolWaitCallback tpTail;

        //The dummy object used to synchronize thread pool queue access. 
        private object tpSync = new object();
 
        //The number of work-items in thread pool queue. 
        private int tpCount;
 
        [System.Security.SecuritySafeCritical]
        public int EnQueue(_ThreadPoolWaitCallback tpcallBack)
        {
            Contract.Assert(!ThreadPoolGlobals.useNewWorkerPool); 

            int count = 0; 
            bool tookLock = false; 
            bool setNativeTpEvent = false;
 
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                Monitor.Enter(tpSync, ref tookLock); 
            }
            finally 
            { 
                if (tookLock)
                { 
                    if (tpCount == 0)
                    {

                        //Indicate to the VM that there is work in this domain. 
                        //Its important to synchronize this notice, otherwise
                        //the VM may not schedule any thread in this domain. 
 
                        setNativeTpEvent = ThreadPool.SetAppDomainRequestActive();
                    } 

                    tpCount++;
                    count = tpCount;
 
                    if (tpHead == null)
                    { 
                        tpHead = tpcallBack; 
                        tpTail = tpcallBack;
                    } 
                    else
                    {
                        tpTail._next = tpcallBack;
                        tpTail = tpcallBack; 
                    }
 
                    Monitor.Exit(tpSync); 

                    if (setNativeTpEvent) 
                    {
                        ThreadPool.SetNativeTpEvent();
                    }
                } 
            }
 
            return count; 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        public int DeQueue(ref _ThreadPoolWaitCallback callback)
        {
            Contract.Assert(!ThreadPoolGlobals.useNewWorkerPool); 

            bool tookLock = false; 
            _ThreadPoolWaitCallback tpWaitCallBack = null; 

            RuntimeHelpers.PrepareConstrainedRegions(); 
            try
            {
                Monitor.Enter(tpSync, ref tookLock);
            } 
            finally
            { 
                if (tookLock) 
                {
                    _ThreadPoolWaitCallback head = tpHead; 
                    if ( head != null)
                    {
                        tpWaitCallBack = head;
                        tpHead = head._next; 
                        tpCount--;
 
                        if(tpCount == 0) 
                        {
                            Contract.Assert(tpHead == null,"TP Queue head expected to be null"); 

                            tpTail = null;

                            //Indicate to the VM that there is no work in this 
                            //domain. Its important to synchronize this notice,
                            //otherwise the VM may keep calling the Managed 
                            //callbacks endlessly. 

                            ThreadPool.ClearAppDomainRequestActive(); 
                        }
                    }

                    Monitor.Exit(tpSync); 
                }
            } 
 
            callback = tpWaitCallBack;
            return tpCount; 
        }

        public int GetQueueCount()
        { 
            Contract.Assert(!ThreadPoolGlobals.useNewWorkerPool);
            return tpCount; 
        } 

    } 
#endif //FEATURE_LEGACY_THREADPOOL

    internal sealed class ThreadPoolWorkQueue
    { 
        // Simple sparsely populated array to allow lock-free reading.
        internal class SparseArray<T> where T : class 
        { 
            private T[] m_array;
 
            internal SparseArray(int initialSize)
            {
                m_array = new T[initialSize];
            } 

            internal T[] Current 
            { 
                get { return m_array; }
            } 

            internal int Add(T e)
            {
                while (true) 
                {
                    T[] array = m_array; 
                    lock (array) 
                    {
                        for (int i = 0; i < array.Length; i++) 
                        {
                            if (array[i] == null)
                            {
                                array[i] = e; 
                                return i;
                            } 
                            else if (i == array.Length - 1) 
                            {
                                // Must resize. If we ----d and lost, we start over again. 
                                if (array != m_array)
                                    continue;

                                T[] newArray = new T[array.Length * 2]; 
                                Array.Copy(array, newArray, i + 1);
                                newArray[i + 1] = e; 
                                m_array = newArray; 
                                return i + 1;
                            } 
                        }
                    }
                }
            } 

            internal void Remove(T e) 
            { 
                T[] array = m_array;
                lock (array) 
                {
                    for (int i = 0; i < m_array.Length; i++)
                    {
                        if (m_array[i] == e) 
                        {
                            m_array[i] = null; 
                            break; 
                        }
                    } 
                }
            }
        }
 
        internal class WorkStealingQueue
        { 
            private const int INITIAL_SIZE = 32; 
            internal IThreadPoolWorkItem[] m_array = new IThreadPoolWorkItem[INITIAL_SIZE];
            private int m_mask = INITIAL_SIZE - 1; 

#if DEBUG
            // in debug builds, start at the end so we exercise the index reset logic.
            private const int START_INDEX = int.MaxValue; 
#else
            private const int START_INDEX = 0; 
#endif 

            private volatile int m_headIndex = START_INDEX; 
            private volatile int m_tailIndex = START_INDEX;

            private SpinLock m_foreignLock = new SpinLock(false);
 
            public void LocalPush(IThreadPoolWorkItem obj)
            { 
                int tail = m_tailIndex; 

                // We're going to increment the tail; if we'll overflow, then we need to reset our counts 
                if (tail == int.MaxValue)
                {
                    bool lockTaken = false;
                    try 
                    {
                        m_foreignLock.Enter(ref lockTaken); 
 
                        if (m_tailIndex == int.MaxValue)
                        { 
                            //
                            // Rather than resetting to zero, we'll just mask off the bits we don't care about.
                            // This way we don't need to rearrange the items already in the queue; they'll be found
                            // correctly exactly where they are.  One subtlety here is that we need to make sure that 
                            // if head is currently < tail, it remains that way.  This happens to just fall out from
                            // the bit-masking, because we only do this if tail == int.MaxValue, meaning that all 
                            // bits are set, so all of the bits we're keeping will also be set.  Thus it's impossible 
                            // for the head to end up > than the tail, since you can't set any more bits than all of
                            // them. 
                            //
                            m_headIndex = m_headIndex & m_mask;
                            m_tailIndex = tail = m_tailIndex & m_mask;
                            Contract.Assert(m_headIndex <= m_tailIndex); 
                        }
                    } 
                    finally 
                    {
                        if (lockTaken) 
                            m_foreignLock.Exit(true);
                    }
                }
 
                // When there are at least 2 elements' worth of space, we can take the fast path.
                if (tail < m_headIndex + m_mask) 
                { 
                    m_array[tail & m_mask] = obj;
                    m_tailIndex = tail + 1; 
                }
                else
                {
                    // We need to contend with foreign pops, so we lock. 
                    bool lockTaken = false;
                    try 
                    { 
                        m_foreignLock.Enter(ref lockTaken);
 
                        int head = m_headIndex;
                        int count = m_tailIndex - m_headIndex;

                        // If there is still space (one left), just add the element. 
                        if (count >= m_mask)
                        { 
                            // We're full; expand the queue by doubling its size. 
                            IThreadPoolWorkItem[] newArray = new IThreadPoolWorkItem[m_array.Length << 1];
                            for (int i = 0; i < m_array.Length; i++) 
                                newArray[i] = m_array[(i + head) & m_mask];

                            // Reset the field values, incl. the mask.
                            m_array = newArray; 
                            m_headIndex = 0;
                            m_tailIndex = tail = count; 
                            m_mask = (m_mask << 1) | 1; 
                        }
 
                        m_array[tail & m_mask] = obj;
                        m_tailIndex = tail + 1;
                    }
                    finally 
                    {
                        if (lockTaken) 
                            m_foreignLock.Exit(false); 
                    }
                } 
            }

            public bool LocalFindAndPop(IThreadPoolWorkItem obj)
            { 
                // Fast path: check the tail. If equal, we can skip the lock.
                if (m_array[(m_tailIndex - 1) & m_mask] == obj) 
                { 
                    IThreadPoolWorkItem unused;
                    if (LocalPop(out unused)) 
                    {
                        Contract.Assert(unused == obj);
                        return true;
                    } 
                    return false;
                } 
 
                // Else, do an O(N) search for the work item. The theory of work stealing and our
                // inlining logic is that most waits will happen on recently queued work.  And 
                // since recently queued work will be close to the tail end (which is where we
                // begin our search), we will likely find it quickly.  In the worst case, we
                // will traverse the whole local queue; this is typically not going to be a
                // problem (although degenerate cases are clearly an issue) because local work 
                // queues tend to be somewhat shallow in length, and because if we fail to find
                // the work item, we are about to block anyway (which is very expensive). 
                for (int i = m_tailIndex - 2; i >= m_headIndex; i--) 
                {
                    if (m_array[i & m_mask] == obj) 
                    {
                        // If we found the element, block out steals to avoid interference.
                        // @
                        bool lockTaken = false; 
                        try
                        { 
                            m_foreignLock.Enter(ref lockTaken); 

                            // If we lost the ----, bail. 
                            if (m_array[i & m_mask] == null)
                            {
                                //Console.WriteLine("{0} Pop(failed)", Thread.CurrentThread.ManagedThreadId);
                                return false; 
                            }
 
                            // Otherwise, null out the element. 
                            m_array[i & m_mask] = null;
 
                            // And then check to see if we can fix up the indexes (if we're at
                            // the edge).  If we can't, we just leave nulls in the array and they'll
                            // get filtered out eventually (but may lead to superflous resizing).
                            if (i == m_tailIndex) 
                                m_tailIndex -= 1;
                            else if (i == m_headIndex) 
                                m_headIndex += 1; 

                            //Console.WriteLine("{0} Pop(success)", Thread.CurrentThread.ManagedThreadId); 
                            return true;
                        }
                        finally
                        { 
                            if (lockTaken)
                                m_foreignLock.Exit(false); 
                        } 
                    }
                } 

                //Console.WriteLine("{0} Pop(failed)", Thread.CurrentThread.ManagedThreadId);
                return false;
            } 

            public bool LocalPop(out IThreadPoolWorkItem obj) 
            { 
                while (true)
                { 
                    // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                    int tail = m_tailIndex;
                    if (m_headIndex >= tail)
                    { 
                        obj = null;
                        return false; 
                    } 

                    tail -= 1; 
                    Interlocked.Exchange(ref m_tailIndex, tail);

                    // If there is no interaction with a take, we can head down the fast path.
                    if (m_headIndex <= tail) 
                    {
                        int idx = tail & m_mask; 
                        obj = m_array[idx]; 

                        // Check for nulls in the array. 
                        if (obj == null) continue;

                        m_array[idx] = null;
                        return true; 
                    }
                    else 
                    { 
                        // Interaction with takes: 0 or 1 elements left.
                        bool lockTaken = false; 
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);
 
                            if (m_headIndex <= tail)
                            { 
                                // Element still available. Take it. 
                                int idx = tail & m_mask;
                                obj = m_array[idx]; 

                                // Check for nulls in the array.
                                if (obj == null) continue;
 
                                m_array[idx] = null;
                                return true; 
                            } 
                            else
                            { 
                                // We lost the ----, element was stolen, restore the tail.
                                m_tailIndex = tail + 1;
                                obj = null;
                                return false; 
                            }
                        } 
                        finally 
                        {
                            if (lockTaken) 
                                m_foreignLock.Exit(false);
                        }
                    }
                } 
            }
 
            public bool TrySteal(out IThreadPoolWorkItem obj, ref bool missedSteal) 
            {
                return TrySteal(out obj, ref missedSteal, 0); // no blocking by default. 
            }

            private bool TrySteal(out IThreadPoolWorkItem obj, ref bool missedSteal, int millisecondsTimeout)
            { 
                obj = null;
 
                while (true) 
                {
                    if (m_headIndex >= m_tailIndex) 
                        return false;

                    bool taken = false;
                    try 
                    {
                        m_foreignLock.TryEnter(millisecondsTimeout, ref taken); 
                        if (taken) 
                        {
                            // Increment head, and ensure read of tail doesn't move before it (fence). 
                            int head = m_headIndex;
                            Interlocked.Exchange(ref m_headIndex, head + 1);

                            if (head < m_tailIndex) 
                            {
                                int idx = head & m_mask; 
                                obj = m_array[idx]; 

                                // Check for nulls in the array. 
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return true; 
                            }
                            else 
                            { 
                                // Failed, restore head.
                                m_headIndex = head; 
                                obj = null;
                                missedSteal = true;
                            }
                        } 
                        else
                        { 
                            missedSteal = true; 
                        }
                    } 
                    finally
                    {
                        if (taken)
                            m_foreignLock.Exit(false); 
                    }
 
                    return false; 
                }
            } 
        }

        internal class QueueSegment
        { 
            // Holds a segment of the queue.  Enqueues/Dequeues start at element 0, and work their way up.
            internal IThreadPoolWorkItem[] nodes; 
            private const int QueueSegmentLength = 256; 

            // Holds the indexes of the lowest and highest valid elements of the nodes array. 
            // The low index is in the lower 16 bits, high index is in the upper 16 bits.
            // Use GetIndexes and CompareExchangeIndexes to manipulate this.
            private volatile int indexes;
 
            // The next segment in the queue.
            public volatile QueueSegment Next; 
 

            const int SixteenBits = 0xffff; 

            void GetIndexes(out int upper, out int lower)
            {
                int i = indexes; 
                upper = (i >> 16) & SixteenBits;
                lower = i & SixteenBits; 
 
                Contract.Assert(upper >= lower);
                Contract.Assert(upper <= nodes.Length); 
                Contract.Assert(lower <= nodes.Length);
                Contract.Assert(upper >= 0);
                Contract.Assert(lower >= 0);
            } 

            bool CompareExchangeIndexes(ref int prevUpper, int newUpper, ref int prevLower, int newLower) 
            { 
                Contract.Assert(newUpper >= newLower);
                Contract.Assert(newUpper <= nodes.Length); 
                Contract.Assert(newLower <= nodes.Length);
                Contract.Assert(newUpper >= 0);
                Contract.Assert(newLower >= 0);
                Contract.Assert(newUpper >= prevUpper); 
                Contract.Assert(newLower >= prevLower);
                Contract.Assert(newUpper == prevUpper ^ newLower == prevLower); 
 
                int oldIndexes = (prevUpper << 16) | (prevLower & SixteenBits);
                int newIndexes = (newUpper << 16) | (newLower & SixteenBits); 
                int prevIndexes = Interlocked.CompareExchange(ref indexes, newIndexes, oldIndexes);
                prevUpper = (prevIndexes >> 16) & SixteenBits;
                prevLower = prevIndexes & SixteenBits;
                return prevIndexes == oldIndexes; 
            }
 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
            public QueueSegment()
            { 
                Contract.Assert(QueueSegmentLength <= SixteenBits);
                nodes = new IThreadPoolWorkItem[QueueSegmentLength];
            }
 

            public bool IsUsedUp() 
            { 
                int upper, lower;
                GetIndexes(out upper, out lower); 
                return (upper == nodes.Length) &&
                       (lower == nodes.Length);
            }
 
            public bool TryEnqueue(IThreadPoolWorkItem node)
            { 
                // 
                // If there's room in this segment, atomically increment the upper count (to reserve
                // space for this node), then store the node. 
                // Note that this leaves a window where it will look like there is data in that
                // array slot, but it hasn't been written yet.  This is taken care of in TryDequeue
                // with a busy-wait loop, waiting for the element to become non-null.  This implies
                // that we can never store null nodes in this data structure. 
                //
                Contract.Assert(null != node); 
 
                int upper, lower;
                GetIndexes(out upper, out lower); 

                while (true)
                {
                    if (upper == nodes.Length) 
                        return false;
 
                    if (CompareExchangeIndexes(ref upper, upper + 1, ref lower, lower)) 
                    {
                        Contract.Assert(nodes[upper] == null); 
                        nodes[upper] = node;
                        return true;
                    }
                } 
            }
 
            public bool TryDequeue(out IThreadPoolWorkItem node) 
            {
                // 
                // If there are nodes in this segment, increment the lower count, then take the
                // element we find there.
                //
                int upper, lower; 
                GetIndexes(out upper, out lower);
 
                while(true) 
                {
                    if (lower == upper) 
                    {
                        node = null;
                        return false;
                    } 

                    if (CompareExchangeIndexes(ref upper, upper, ref lower, lower + 1)) 
                    { 
                        // It's possible that a concurrent call to Enqueue hasn't yet
                        // written the node reference to the array.  We need to spin until 
                        // it shows up.
                        SpinWait spinner = new SpinWait();
                        while (nodes[lower] == null)
                            spinner.SpinOnce(); 

                        node = nodes[lower]; 
 
                        // Null-out the reference so the object can be GC'd earlier.
                        nodes[lower] = null; 

                        return true;
                    }
                } 
            }
        } 
 
        // The head and tail of the queue.  We enqueue to the head, and dequeue from the tail.
        internal volatile QueueSegment queueHead; 
        internal volatile QueueSegment queueTail;

        internal static SparseArray<WorkStealingQueue> allThreadQueues = new SparseArray<WorkStealingQueue>(16); //
 
        private volatile int numOutstandingThreadRequests = 0;
 
        public ThreadPoolWorkQueue() 
        {
            queueTail = queueHead = new QueueSegment(); 
        }

        [SecurityCritical]
        public ThreadPoolWorkQueueThreadLocals EnsureCurrentThreadHasQueue() 
        {
            if (null == ThreadPoolWorkQueueThreadLocals.threadLocals) 
                ThreadPoolWorkQueueThreadLocals.threadLocals = new ThreadPoolWorkQueueThreadLocals(this); 
            return ThreadPoolWorkQueueThreadLocals.threadLocals;
        } 

        [SecurityCritical]
        internal void EnsureThreadRequested()
        { 
            //
            // If we have not yet requested #procs threads from the VM, then request a new thread. 
            // Note that there is a separate count in the VM which will also be incremented in this case, 
            // which is handled by AdjustThreadsInPool.
            // 
            int count = numOutstandingThreadRequests;
            while (count < ThreadPoolGlobals.processorCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count+1, count); 
                if (prev == count)
                { 
                    ThreadPool.AdjustThreadsInPool(1); 
                    break;
                } 
                count = prev;
            }
        }
 
        [SecurityCritical]
        internal void MarkThreadRequestSatisfied() 
        { 
            //
            // The VM has called us, so one of our outstanding thread requests has been satisfied. 
            // Decrement the count so that future calls to EnsureThreadRequested will succeed.
            // Note that there is a separate count in the VM which has already been decremented by the VM
            // by the time we reach this point.
            // 
            int count = numOutstandingThreadRequests;
            while (count > 0) 
            { 
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count - 1, count);
                if (prev == count) 
                {
                    break;
                }
                count = prev; 
            }
        } 
 
        [SecurityCritical]
        public void Enqueue(IThreadPoolWorkItem callback, bool forceGlobal) 
        {
#if FEATURE_LEGACY_THREADPOOL
            Contract.Assert(ThreadPoolGlobals.useNewWorkerPool);
#endif //FEATURE_LEGACY_THREADPOOL 

            ThreadPoolWorkQueueThreadLocals tl = null; 
            if (!forceGlobal) 
                tl = ThreadPoolWorkQueueThreadLocals.threadLocals;
 
            if (null != tl)
            {
                //Console.WriteLine("{0} Enqueue(local)", Thread.CurrentThread.ManagedThreadId);
                tl.workStealingQueue.LocalPush(callback); 
            }
            else 
            { 
                //Console.WriteLine("{0} Enqueue(global)", Thread.CurrentThread.ManagedThreadId);
                QueueSegment head = queueHead; 

                while (!head.TryEnqueue(callback))
                {
                    Interlocked.CompareExchange(ref head.Next, new QueueSegment(), null); 

                    while (head.Next != null) 
                    { 
                        Interlocked.CompareExchange(ref queueHead, head.Next, head);
                        head = queueHead; 
                    }
                }
            }
 
            EnsureThreadRequested();
        } 
 
        [SecurityCritical]
        internal bool LocalFindAndPop(IThreadPoolWorkItem callback) 
        {
#if FEATURE_LEGACY_THREADPOOL
            Contract.Assert(ThreadPoolGlobals.useNewWorkerPool);
#endif //FEATURE_LEGACY_THREADPOOL 

            ThreadPoolWorkQueueThreadLocals tl = ThreadPoolWorkQueueThreadLocals.threadLocals; 
            if (null == tl) 
                return false;
 
            return tl.workStealingQueue.LocalFindAndPop(callback);
        }

        [SecurityCritical] 
        public void Dequeue(ThreadPoolWorkQueueThreadLocals tl, out IThreadPoolWorkItem callback, out bool missedSteal)
        { 
#if FEATURE_LEGACY_THREADPOOL 
            Contract.Assert(ThreadPoolGlobals.useNewWorkerPool);
#endif //FEATURE_LEGACY_THREADPOOL 

            callback = null;
            missedSteal = false;
            WorkStealingQueue wsq = tl.workStealingQueue; 

            if (wsq.LocalPop(out callback)) 
            { 
                //Console.WriteLine("{0} Dequeue(local)", Thread.CurrentThread.ManagedThreadId);
                Contract.Assert(null != callback); 
            }

            if (null == callback)
            { 
                QueueSegment tail = queueTail;
                while (true) 
                { 
                    if (tail.TryDequeue(out callback))
                    { 
                        //Console.WriteLine("{0} Dequeue(global)", Thread.CurrentThread.ManagedThreadId);
                        Contract.Assert(null != callback);
                        break;
                    } 

                    if (null == tail.Next || !tail.IsUsedUp()) 
                    { 
                        break;
                    } 
                    else
                    {
                        Interlocked.CompareExchange(ref queueTail, tail.Next, tail);
                        tail = queueTail; 
                    }
                } 
            } 

            if (null == callback) 
            {
                WorkStealingQueue[] otherQueues = allThreadQueues.Current;
                int i = tl.random.Next(otherQueues.Length);
                int c = otherQueues.Length; 
                while (c > 0)
                { 
                    WorkStealingQueue otherQueue = otherQueues[i % otherQueues.Length]; 
                    if (otherQueue != null &&
                        otherQueue != wsq && 
                        otherQueue.TrySteal(out callback, ref missedSteal))
                    {
                        //Console.WriteLine("{0} Dequeue(steal)", Thread.CurrentThread.ManagedThreadId);
                        Contract.Assert(null != callback); 
                        break;
                    } 
                    i++; 
                    c--;
                } 
            }

            //if (null == callback)
                //Console.WriteLine("{0} Dequeue(failed!)", Thread.CurrentThread.ManagedThreadId); 
        }
 
        [SecurityCritical] 
        static internal bool Dispatch()
        { 
            //
            // The clock is ticking!  We have ThreadPoolGlobals.tpQuantum milliseconds to get some work done, and then
            // we need to return to the VM.
            // 
            int quantumStartTime = Environment.TickCount;
 
            // 
            // Update our records to indicate that an outstanding request for a thread has now been fulfilled.
            // From this point on, we are responsible for requesting another thread if we stop working for any 
            // reason, and we believe there might still be work in the queue.
            //
            // Note that if this thread is aborted before we get a chance to request another one, the VM will
            // record a thread request on our behalf.  So we don't need to worry about getting aborted right here. 
            //
            ThreadPoolGlobals.workQueue.MarkThreadRequestSatisfied(); 
 
            //
            // Assume that we're going to need another thread if this one returns to the VM.  We'll set this to 
            // false later, but only if we're absolutely certain that the queue is empty.
            //
            bool needAnotherThread = true;
            IThreadPoolWorkItem workItem = null; 
            try
            { 
                // 
                // Set up our thread-local data
                // 
                ThreadPoolWorkQueueThreadLocals tl = ThreadPoolGlobals.workQueue.EnsureCurrentThreadHasQueue();

                //
                // Loop until our quantum expires. 
                //
                while ((Environment.TickCount - quantumStartTime) < ThreadPoolGlobals.tpQuantum) 
                { 
                    //
                    // Dequeue and EnsureThreadRequested must be protected from ThreadAbortException. 
                    // These are fast, so this will not delay aborts/AD-unloads for very long.
                    //
                    try { }
                    finally 
                    {
                        bool missedSteal = false; 
                        ThreadPoolGlobals.workQueue.Dequeue(tl, out workItem, out missedSteal); 

                        if (workItem == null) 
                        {
                            //
                            // No work.  We're going to return to the VM once we leave this protected region.
                            // If we missed a steal, though, there may be more work in the queue. 
                            // Instead of looping around and trying again, we'll just request another thread.  This way
                            // we won't starve other AppDomains while we spin trying to get locks, and hopefully the thread 
                            // that owns the contended work-stealing queue will pick up its own workitems in the meantime, 
                            // which will be more efficient than this thread doing it anyway.
                            // 
                            needAnotherThread = missedSteal;
                        }
                        else
                        { 
                            //
                            // If we found work, there may be more work.  Ask for another thread so that the other work can be processed 
                            // in parallel.  Note that this will only ask for a max of #procs threads, so it's safe to call it for every dequeue. 
                            //
                            ThreadPoolGlobals.workQueue.EnsureThreadRequested(); 
                        }
                    }

                    if (workItem == null) 
                    {
                        // Tell the VM we're returning normally, not because Hill Climbing asked us to return. 
                        return true; 
                    }
                    else 
                    {
                        //
                        // Execute the workitem outside of any finally blocks, so that it can be aborted if needed.
                        // 
                        if (ThreadPoolGlobals.enableWorkerTracking)
                        { 
                            bool reportedStatus = false; 
                            try
                            { 
                                try { }
                                finally
                                {
                                    ThreadPool.ReportThreadStatus(true); 
                                    reportedStatus = true;
                                } 
                                workItem.ExecuteWorkItem(); 
                                workItem = null;
                            } 
                            finally
                            {
                                if (reportedStatus)
                                    ThreadPool.ReportThreadStatus(false); 
                            }
                        } 
                        else 
                        {
                            workItem.ExecuteWorkItem(); 
                            workItem = null;
                        }

                        // 
                        // Notify the VM that we executed this workitem.  This is also our opportunity to ask whether Hill Climbing wants
                        // us to return the thread to the pool or not. 
                        // 
                        if (!ThreadPool.NotifyWorkItemComplete())
                            return false; 
                    }
                }

                // If we get here, it's because our quantum expired.  Tell the VM we're returning normally. 
                return true;
            } 
            catch (ThreadAbortException tae) 
            {
                // 
                // This is here to catch the case where this thread is aborted between the time we exit the finally block in the dispatch
                // loop, and the time we execute the work item.  QueueUserWorkItemCallback uses this to update its accounting of whether
                // it was executed or not (in debug builds only).  Task uses this to communicate the ThreadAbortException to anyone
                // who waits for the task to complete. 
                //
                if (workItem != null) 
                    workItem.MarkAborted(tae); 

                // 
                // In this case, the VM is going to request another thread on our behalf.  No need to do it twice.
                //
                needAnotherThread = false;
                throw; 
            }
            finally 
            { 
                //
                // If we are exiting for any reason other than that the queue is definitely empty, ask for another 
                // thread to pick up where we left off.
                //
                if (needAnotherThread)
                    ThreadPoolGlobals.workQueue.EnsureThreadRequested(); 
            }
        } 
    } 

    // Holds a WorkStealingQueue, and remmoves it from the list when this object is no longer referened. 
    internal sealed class ThreadPoolWorkQueueThreadLocals
    {
        [ThreadStatic]
        [SecurityCritical] 
        public static ThreadPoolWorkQueueThreadLocals threadLocals;
 
        public readonly ThreadPoolWorkQueue workQueue; 
        public readonly ThreadPoolWorkQueue.WorkStealingQueue workStealingQueue;
        public readonly Random random = new Random(Thread.CurrentThread.ManagedThreadId); 

        public ThreadPoolWorkQueueThreadLocals(ThreadPoolWorkQueue tpq)
        {
            workQueue = tpq; 
            workStealingQueue = new ThreadPoolWorkQueue.WorkStealingQueue();
            ThreadPoolWorkQueue.allThreadQueues.Add(workStealingQueue); 
        } 

        [SecurityCritical] 
        private void CleanUp()
        {
            if (null != workStealingQueue)
            { 
                if (null != workQueue)
                { 
                    bool done = false; 
                    while (!done)
                    { 
                        // Ensure that we won't be aborted between LocalPop and Enqueue.
                        try { }
                        finally
                        { 
                            IThreadPoolWorkItem cb = null;
                            if (workStealingQueue.LocalPop(out cb)) 
                            { 
                                Contract.Assert(null != cb);
                                workQueue.Enqueue(cb, true); 
                            }
                            else
                            {
                                done = true; 
                            }
                        } 
                    } 
                }
 
                ThreadPoolWorkQueue.allThreadQueues.Remove(workStealingQueue);
            }
        }
 
        [SecuritySafeCritical]
        ~ThreadPoolWorkQueueThreadLocals() 
        { 
            // Since the purpose of calling CleanUp is to transfer any pending workitems into the global
            // queue so that they will be executed by another thread, there's no point in doing this cleanup 
            // if we're in the process of shutting down or unloading the AD.  In those cases, the work won't
            // execute anyway.  And there are subtle ----s involved there that would lead us to do the wrong
            // thing anyway.  So we'll only clean up if this is a "normal" finalization.
            if (!(Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload())) 
                CleanUp();
        } 
    } 

    internal sealed class RegisteredWaitHandleSafe : CriticalFinalizerObject 
    {
        private static IntPtr InvalidHandle
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            { 
                return Win32Native.INVALID_HANDLE_VALUE; 
            }
        } 
        private IntPtr registeredWaitHandle;
        private WaitHandle m_internalWaitObject;
        private bool bReleaseNeeded = false;
        private int m_lock = 0; 

        internal RegisteredWaitHandleSafe() 
        { 
            registeredWaitHandle = InvalidHandle;
        } 

        internal IntPtr GetHandle()
        {
           return registeredWaitHandle; 
        }
 
        internal void SetHandle(IntPtr handle) 
        {
            registeredWaitHandle = handle; 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        internal void SetWaitObject(WaitHandle waitObject) 
        {
            // needed for DangerousAddRef 
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            } 
            finally
            { 
                m_internalWaitObject = waitObject; 
                if (waitObject != null)
                { 
                    m_internalWaitObject.SafeWaitHandle.DangerousAddRef(ref bReleaseNeeded);
                }
            }
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        internal bool Unregister(
             WaitHandle     waitObject          // object to be notified when all callbacks to delegates have completed
             )
        { 
            bool result = false;
            // needed for DangerousRelease 
            RuntimeHelpers.PrepareConstrainedRegions(); 
            try
            { 
            }
            finally
            {
                // lock(this) cannot be used reliably in Cer since thin lock could be 
                // promoted to syncblock and that is not a guaranteed operation
                bool bLockTaken = false; 
                do 
                {
                    if (Interlocked.CompareExchange(ref m_lock, 1, 0) == 0) 
                    {
                        bLockTaken = true;
                        try
                        { 
                            if (ValidHandle())
                            { 
                                result = UnregisterWaitNative(GetHandle(), waitObject == null ? null : waitObject.SafeWaitHandle); 
                                if (result == true)
                                { 
                                    if (bReleaseNeeded)
                                    {
                                        m_internalWaitObject.SafeWaitHandle.DangerousRelease();
                                        bReleaseNeeded = false; 
                                    }
                                    // if result not true don't release/suppress here so finalizer can make another attempt 
                                    SetHandle(InvalidHandle); 
                                    m_internalWaitObject = null;
                                    GC.SuppressFinalize(this); 
                                }
                            }
                        }
                        finally 
                        {
                            m_lock = 0; 
                        } 
                    }
                    Thread.SpinWait(1);     // yield to processor 
                }
                while (!bLockTaken);
            }
            return result; 
        }
 
        private bool ValidHandle() 
        {
            return (registeredWaitHandle != InvalidHandle && registeredWaitHandle != IntPtr.Zero); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        ~RegisteredWaitHandleSafe() 
        { 
            // if the app has already unregistered the wait, there is nothing to cleanup
            // we can detect this by checking the handle. Normally, there is no ---- here 
            // so no need to protect reading of handle. However, if this object gets
            // resurrected and then someone does an unregister, it would introduce a ----

            // PrepareConstrainedRegions call not needed since finalizer already in Cer 

            // lock(this) cannot be used reliably even in Cer since thin lock could be 
            // promoted to syncblock and that is not a guaranteed operation 

            bool bLockTaken = false; 
            do
            {
                if (Interlocked.CompareExchange(ref m_lock, 1, 0) == 0)
                { 
                    bLockTaken = true;
                    try 
                    { 
                        if (ValidHandle())
                        { 
                            WaitHandleCleanupNative(registeredWaitHandle);
                            if (bReleaseNeeded)
                            {
                                m_internalWaitObject.SafeWaitHandle.DangerousRelease(); 
                                bReleaseNeeded = false;
                            } 
                            SetHandle(InvalidHandle); 
                            m_internalWaitObject = null;
                        } 
                    }
                    finally
                    {
                        m_lock = 0; 
                    }
                } 
                Thread.SpinWait(1);     // yield to processor 
            }
            while (!bLockTaken); 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void WaitHandleCleanupNative(IntPtr handle); 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern bool UnregisterWaitNative(IntPtr handle, SafeHandle waitObject);
    }
 
[System.Runtime.InteropServices.ComVisible(true)]
#if FEATURE_REMOTING 
    public sealed class RegisteredWaitHandle : MarshalByRefObject { 
#if false
    } 
#endif // FALSE
#else // FEATURE_REMOTING
    public sealed class RegisteredWaitHandle {
#endif // FEATURE_REMOTING 
        private RegisteredWaitHandleSafe internalRegisteredWait;
 
        internal RegisteredWaitHandle() 
        {
            internalRegisteredWait = new RegisteredWaitHandleSafe(); 
        }

        internal void SetHandle(IntPtr handle)
        { 
           internalRegisteredWait.SetHandle(handle);
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        internal void SetWaitObject(WaitHandle waitObject) 
        {
           internalRegisteredWait.SetWaitObject(waitObject);
        }
 

[System.Security.SecuritySafeCritical]  // auto-generated 
[System.Runtime.InteropServices.ComVisible(true)] 
        // This is the only public method on this class
        public bool Unregister( 
             WaitHandle     waitObject          // object to be notified when all callbacks to delegates have completed
             )
        {
            return internalRegisteredWait.Unregister(waitObject); 
        }
    } 
 
    [System.Runtime.InteropServices.ComVisible(true)]
    public delegate void WaitCallback(Object state); 

    [System.Runtime.InteropServices.ComVisible(true)]
    public delegate void WaitOrTimerCallback(Object state, bool timedOut);  // signalled or timed out
 
    internal class _ThreadPoolWaitCallback
    { 
#if FEATURE_LEGACY_THREADPOOL 
        WaitCallback _waitCallback;
        ExecutionContext _executionContext; 
        Object _state;

        //ThreadPoolWaitCallBack is the unit of thread pool work, and is
        //chained together. The _next field is the link. This field should be 
        //accessible to the Thread pool queue in order to reduce number of
        //object allocations as this is on a perf-critical path. 
 
        protected internal _ThreadPoolWaitCallback _next;
 
        static internal ContextCallback _ccb = new ContextCallback(WaitCallback_Context);
        static internal void WaitCallback_Context(Object state)
        {
            _ThreadPoolWaitCallback obj = (_ThreadPoolWaitCallback)state; 
            obj._waitCallback(obj._state);
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        internal _ThreadPoolWaitCallback(WaitCallback waitCallback, Object state, bool compressStack, ref StackCrawlMark stackMark) 
        {
            _waitCallback = waitCallback;
            _state = state;
            if (compressStack && !ExecutionContext.IsFlowSuppressed()) 
            {
                // clone the exection context 
                _executionContext = ExecutionContext.Capture( 
                    ref stackMark,
                    ExecutionContext.CaptureOptions.IgnoreSyncCtx | ExecutionContext.CaptureOptions.OptimizeDefaultCase); 
            }
        }

#endif //FEATURE_LEGACY_THREADPOOL 

        // call back helper 
        // This function dispatches requests to the user-callbacks. The 
        // work-items are fetched from the per-appdomain queue in a loop until
        // either there is no more work or the quantum has expired. The quantum 
        // is enforced to maintain fairness among appdomains.
        [System.Security.SecurityCritical]  // auto-generated
        static internal bool PerformWaitCallback()
        { 
#if FEATURE_LEGACY_THREADPOOL
            if (ThreadPoolGlobals.useNewWorkerPool) 
#endif //FEATURE_LEGACY_THREADPOOL 
                return ThreadPoolWorkQueue.Dispatch();
 
#if FEATURE_LEGACY_THREADPOOL
            int totTime = 0;

            _ThreadPoolWaitCallback tpWaitCallBack = null; 

            int startTime = Environment.TickCount; 
 
            do
            { 
                int count = ThreadPoolGlobals.tpQueue.DeQueue(ref tpWaitCallBack);

                if (tpWaitCallBack == null)
                { 
                    break;
                } 
 
                // This call in the VM updates queue counts, number of
                // completed requests, etc. More importantly, it also 
                // resets thread state like CriticalRegionCount, priority etc.
                //

                ThreadPool.CompleteThreadPoolRequest((uint)count); 

                PerformWaitCallbackInternal(tpWaitCallBack); 
 
                int endTime = Environment.TickCount;
 
                totTime = (endTime - startTime);

                // Check to see if quantum has expired.
 
                if (totTime > ThreadPoolGlobals.tpQuantum)
                { 
                    if(ThreadPool.ShouldReturnToVm()) 
                    {
                        break; 
                    }
                }

             } while (true); 

            return true; 
#endif //FEATURE_LEGACY_THREADPOOL 
        }
 

#if FEATURE_LEGACY_THREADPOOL
        [System.Security.SecurityCritical]  // auto-generated
        static internal void PerformWaitCallbackInternal(_ThreadPoolWaitCallback tpWaitCallBack) 
        {
 
            // call directly if it is an unsafe call OR EC flow is suppressed 
            if (tpWaitCallBack._executionContext == null)
            { 
                WaitCallback callback = tpWaitCallBack._waitCallback;
                callback(tpWaitCallBack._state);
            }
            else 
            {
                ExecutionContext.Run(tpWaitCallBack._executionContext, _ccb, tpWaitCallBack, true); 
            } 
        }
#endif //FEATURE_LEGACY_THREADPOOL 
    }

    //
    // Interface to something that can be queued to the TP.  This is implemented by 
    // QueueUserWorkItemCallback and Task.  If we decide to expose some of the workstealing
    // stuff, this is NOT the thing we want to expose to the public. 
    // 
    internal interface IThreadPoolWorkItem
    { 
        [SecurityCritical]
        void ExecuteWorkItem();
        [SecurityCritical]
        void MarkAborted(ThreadAbortException tae); 
    }
 
    internal sealed class QueueUserWorkItemCallback : IThreadPoolWorkItem 
    {
        private WaitCallback callback; 
        private ExecutionContext context;
        private Object state;

#if DEBUG 
        volatile int executed;
 
        ~QueueUserWorkItemCallback() 
        {
            Contract.Assert( 
                executed != 0 || Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload(),
                "A QueueUserWorkItemCallback was never called!");
        }
 
        void MarkExecuted(bool aborted)
        { 
            GC.SuppressFinalize(this); 
            Contract.Assert(
                0 == Interlocked.Exchange(ref executed, 1) || aborted, 
                "A QueueUserWorkItemCallback was called twice!");
        }
#endif
 
        [SecurityCritical]
        internal QueueUserWorkItemCallback(WaitCallback waitCallback, Object stateObj, bool compressStack, ref StackCrawlMark stackMark) 
        { 
            callback = waitCallback;
            state = stateObj; 
            if (compressStack && !ExecutionContext.IsFlowSuppressed())
            {
                // clone the exection context
                context = ExecutionContext.Capture( 
                    ref stackMark,
                    ExecutionContext.CaptureOptions.IgnoreSyncCtx | ExecutionContext.CaptureOptions.OptimizeDefaultCase); 
            } 
        }
 
        //
        // internal test hook - used by tests to exercise work-stealing, etc.
        //
        internal QueueUserWorkItemCallback(WaitCallback waitCallback, Object stateObj, ExecutionContext ec) 
        {
            callback = waitCallback; 
            state = stateObj; 
            context = ec;
        } 

        [SecurityCritical]
        void IThreadPoolWorkItem.ExecuteWorkItem()
        { 
#if DEBUG
            MarkExecuted(false); 
#endif 

            // call directly if it is an unsafe call OR EC flow is suppressed 
            if (context == null)
            {
                WaitCallback cb = callback;
                callback = null; 
                cb(state);
            } 
            else 
            {
                ExecutionContext.Run(context, ccb, this, true); 
            }
        }

        [SecurityCritical] 
        void IThreadPoolWorkItem.MarkAborted(ThreadAbortException tae)
        { 
#if DEBUG 
            // this workitem didn't execute because we got a ThreadAbortException prior to the call to ExecuteWorkItem.
            // This counts as being executed for our purposes. 
            MarkExecuted(true);
#endif
        }
 
        static internal ContextCallback ccb = new ContextCallback(WaitCallback_Context);
 
        static internal void WaitCallback_Context(Object state) 
        {
            QueueUserWorkItemCallback obj = (QueueUserWorkItemCallback)state; 
            WaitCallback wc = obj.callback as WaitCallback;
            Contract.Assert(null != wc);
            wc(obj.state);
        } 
    }
 
    internal class _ThreadPoolWaitOrTimerCallback 
    {
        WaitOrTimerCallback _waitOrTimerCallback; 
        ExecutionContext _executionContext;
        Object _state;
        static private ContextCallback _ccbt = new ContextCallback(WaitOrTimerCallback_Context_t);
        static private ContextCallback _ccbf = new ContextCallback(WaitOrTimerCallback_Context_f); 

        [System.Security.SecurityCritical]  // auto-generated 
        internal _ThreadPoolWaitOrTimerCallback(WaitOrTimerCallback waitOrTimerCallback, Object state, bool compressStack, ref StackCrawlMark stackMark) 
        {
            _waitOrTimerCallback = waitOrTimerCallback; 
            _state = state;

            if (compressStack && !ExecutionContext.IsFlowSuppressed())
            { 
                // capture the exection context
                _executionContext = ExecutionContext.Capture( 
                    ref stackMark, 
                    ExecutionContext.CaptureOptions.IgnoreSyncCtx | ExecutionContext.CaptureOptions.OptimizeDefaultCase);
            } 
        }

        static private void WaitOrTimerCallback_Context_t(Object state)
        { 
            WaitOrTimerCallback_Context(state, true);
        } 
        static private void WaitOrTimerCallback_Context_f(Object state) 
        {
            WaitOrTimerCallback_Context(state, false); 
        }

        static private void WaitOrTimerCallback_Context(Object state, bool timedOut)
        { 
            _ThreadPoolWaitOrTimerCallback helper = (_ThreadPoolWaitOrTimerCallback)state;
            helper._waitOrTimerCallback(helper._state, timedOut); 
        } 

        // call back helper 
        [System.Security.SecurityCritical]  // auto-generated
        static internal void PerformWaitOrTimerCallback(Object state, bool timedOut)
        {
            _ThreadPoolWaitOrTimerCallback helper = (_ThreadPoolWaitOrTimerCallback)state; 
            Contract.Assert(helper != null, "Null state passed to PerformWaitOrTimerCallback!");
            // call directly if it is an unsafe call OR EC flow is suppressed 
            if (helper._executionContext == null) 
            {
                WaitOrTimerCallback callback = helper._waitOrTimerCallback; 
                callback(helper._state, timedOut);
            }
            else
            { 
                using (ExecutionContext executionContext = helper._executionContext.CreateCopy())
                { 
                if (timedOut) 
                        ExecutionContext.Run(executionContext, _ccbt, helper, true);
                else 
                        ExecutionContext.Run(executionContext, _ccbf, helper, true);
                }
            }
        } 

    } 
 
    [System.Security.SecurityCritical]
    [CLSCompliant(false)] 
    [System.Runtime.InteropServices.ComVisible(true)]
    unsafe public delegate void IOCompletionCallback(uint errorCode, // Error code
                                       uint numBytes, // No. of bytes transferred
                                       NativeOverlapped* pOVERLAP // ptr to OVERLAP structure 
                                       );
 
    [HostProtection(Synchronization=true, ExternalThreading=true)] 
    public static class ThreadPool
    { 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [SecurityPermissionAttribute(SecurityAction.Demand, ControlThread = true)]
        public static bool SetMaxThreads(int workerThreads, int completionPortThreads) 
        {
            return SetMaxThreadsNative(workerThreads, completionPortThreads); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public static void GetMaxThreads(out int workerThreads, out int completionPortThreads)
        {
            GetMaxThreadsNative(out workerThreads, out completionPortThreads);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [SecurityPermissionAttribute(SecurityAction.Demand, ControlThread = true)] 
        public static bool SetMinThreads(int workerThreads, int completionPortThreads)
        { 
            return SetMinThreadsNative(workerThreads, completionPortThreads);
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public static void GetMinThreads(out int workerThreads, out int completionPortThreads)
        { 
            GetMinThreadsNative(out workerThreads, out completionPortThreads); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads)
        {
            GetAvailableThreadsNative(out workerThreads, out completionPortThreads); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [CLSCompliant(false)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static RegisteredWaitHandle RegisterWaitForSingleObject(  // throws RegisterWaitException
             WaitHandle             waitObject,
             WaitOrTimerCallback    callBack,
             Object                 state, 
             uint               millisecondsTimeOutInterval,
             bool               executeOnlyOnce    // NOTE: we do not allow other options that allow the callback to be queued as an APC 
             ) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RegisterWaitForSingleObject(waitObject,callBack,state,millisecondsTimeOutInterval,executeOnlyOnce,ref stackMark,true);
        }

        [System.Security.SecurityCritical]  // auto-generated_required 
        [CLSCompliant(false)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(  // throws RegisterWaitException 
             WaitHandle             waitObject,
             WaitOrTimerCallback    callBack, 
             Object                 state,
             uint               millisecondsTimeOutInterval,
             bool               executeOnlyOnce    // NOTE: we do not allow other options that allow the callback to be queued as an APC
             ) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RegisterWaitForSingleObject(waitObject,callBack,state,millisecondsTimeOutInterval,executeOnlyOnce,ref stackMark,false); 
        }
 

        [System.Security.SecurityCritical]  // auto-generated
        private static RegisteredWaitHandle RegisterWaitForSingleObject(  // throws RegisterWaitException
             WaitHandle             waitObject, 
             WaitOrTimerCallback    callBack,
             Object                 state, 
             uint               millisecondsTimeOutInterval, 
             bool               executeOnlyOnce,   // NOTE: we do not allow other options that allow the callback to be queued as an APC
             ref StackCrawlMark stackMark, 
             bool               compressStack
             )
        {
#if FEATURE_REMOTING 
            if (RemotingServices.IsTransparentProxy(waitObject))
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_WaitOnTransparentProxy")); 
            Contract.EndContractBlock(); 
#endif
 
            RegisteredWaitHandle registeredWaitHandle = new RegisteredWaitHandle();

            if (callBack != null)
            { 
                _ThreadPoolWaitOrTimerCallback callBackHelper = new _ThreadPoolWaitOrTimerCallback(callBack, state, compressStack, ref stackMark);
                state = (Object)callBackHelper; 
                // call SetWaitObject before native call so that waitObject won't be closed before threadpoolmgr registration 
                // this could occur if callback were to fire before SetWaitObject does its addref
                registeredWaitHandle.SetWaitObject(waitObject); 
                IntPtr nativeRegisteredWaitHandle = RegisterWaitForSingleObjectNative(waitObject,
                                                                               state,
                                                                               millisecondsTimeOutInterval,
                                                                               executeOnlyOnce, 
                                                                               registeredWaitHandle,
                                                                               ref stackMark, 
                                                                               compressStack); 
                registeredWaitHandle.SetHandle(nativeRegisteredWaitHandle);
            } 
            else
            {
                throw new ArgumentNullException("WaitOrTimerCallback");
            } 
            return registeredWaitHandle;
        } 
 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static RegisteredWaitHandle RegisterWaitForSingleObject(  // throws RegisterWaitException
             WaitHandle             waitObject,
             WaitOrTimerCallback    callBack, 
             Object                 state,
             int                    millisecondsTimeOutInterval, 
             bool               executeOnlyOnce    // NOTE: we do not allow other options that allow the callback to be queued as an APC 
             )
        { 
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException("millisecondsTimeOutInterval", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            Contract.EndContractBlock();
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RegisterWaitForSingleObject(waitObject,callBack,state,(UInt32)millisecondsTimeOutInterval,executeOnlyOnce,ref stackMark,true);
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(  // throws RegisterWaitException
             WaitHandle             waitObject,
             WaitOrTimerCallback    callBack,
             Object                 state, 
             int                    millisecondsTimeOutInterval,
             bool               executeOnlyOnce    // NOTE: we do not allow other options that allow the callback to be queued as an APC 
             ) 
        {
            if (millisecondsTimeOutInterval < -1) 
                throw new ArgumentOutOfRangeException("millisecondsTimeOutInterval", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            Contract.EndContractBlock();
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RegisterWaitForSingleObject(waitObject,callBack,state,(UInt32)millisecondsTimeOutInterval,executeOnlyOnce,ref stackMark,false); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static RegisteredWaitHandle RegisterWaitForSingleObject(  // throws RegisterWaitException 
            WaitHandle          waitObject,
            WaitOrTimerCallback callBack,
            Object                  state,
            long                    millisecondsTimeOutInterval, 
            bool                executeOnlyOnce    // NOTE: we do not allow other options that allow the callback to be queued as an APC
        ) 
        { 
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException("millisecondsTimeOutInterval", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1")); 
            Contract.EndContractBlock();
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RegisterWaitForSingleObject(waitObject,callBack,state,(UInt32)millisecondsTimeOutInterval,executeOnlyOnce,ref stackMark,true);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(  // throws RegisterWaitException
            WaitHandle          waitObject, 
            WaitOrTimerCallback callBack,
            Object                  state,
            long                    millisecondsTimeOutInterval,
            bool                executeOnlyOnce    // NOTE: we do not allow other options that allow the callback to be queued as an APC 
        )
        { 
            if (millisecondsTimeOutInterval < -1) 
                throw new ArgumentOutOfRangeException("millisecondsTimeOutInterval", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            Contract.EndContractBlock(); 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RegisterWaitForSingleObject(waitObject,callBack,state,(UInt32)millisecondsTimeOutInterval,executeOnlyOnce,ref stackMark,false);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static RegisteredWaitHandle RegisterWaitForSingleObject( 
                          WaitHandle            waitObject,
                          WaitOrTimerCallback   callBack, 
                          Object                state,
                          TimeSpan              timeout,
                          bool                  executeOnlyOnce
                          ) 
        {
            long tm = (long)timeout.TotalMilliseconds; 
            if (tm < -1) 
                throw new ArgumentOutOfRangeException("timeout", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (tm > (long) Int32.MaxValue) 
                throw new ArgumentOutOfRangeException("timeout", Environment.GetResourceString("ArgumentOutOfRange_LessEqualToIntegerMaxVal"));
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RegisterWaitForSingleObject(waitObject,callBack,state,(UInt32)tm,executeOnlyOnce,ref stackMark,true);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
                          WaitHandle            waitObject, 
                          WaitOrTimerCallback   callBack,
                          Object                state,
                          TimeSpan              timeout,
                          bool                  executeOnlyOnce 
                          )
        { 
            long tm = (long)timeout.TotalMilliseconds; 
            if (tm < -1)
                throw new ArgumentOutOfRangeException("timeout", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1")); 
            if (tm > (long) Int32.MaxValue)
                throw new ArgumentOutOfRangeException("timeout", Environment.GetResourceString("ArgumentOutOfRange_LessEqualToIntegerMaxVal"));
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RegisterWaitForSingleObject(waitObject,callBack,state,(UInt32)tm,executeOnlyOnce,ref stackMark,false); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static bool QueueUserWorkItem( 
             WaitCallback           callBack,     // NOTE: we do not expose options that allow the callback to be queued as an APC
             Object                 state
             )
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return QueueUserWorkItemHelper(callBack,state,ref stackMark,true); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static bool QueueUserWorkItem(
             WaitCallback           callBack     // NOTE: we do not expose options that allow the callback to be queued as an APC
             ) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return QueueUserWorkItemHelper(callBack,null,ref stackMark,true); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static bool UnsafeQueueUserWorkItem(
             WaitCallback           callBack,     // NOTE: we do not expose options that allow the callback to be queued as an APC 
             Object                 state
             ) 
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return QueueUserWorkItemHelper(callBack,state,ref stackMark,false); 
        }

        //ThreadPool has per-appdomain managed queue of work-items. The VM is
        //responsible for just scheduling threads into appdomains. After that 
        //work-items are dispatched from the managed queue.
        [System.Security.SecurityCritical]  // auto-generated 
        private static bool QueueUserWorkItemHelper(WaitCallback callBack, Object state, ref StackCrawlMark stackMark, bool compressStack ) 
        {
            bool success =  true; 

            if (callBack != null)
            {
                        //The thread pool maintains a per-appdomain managed work queue. 
                //New thread pool entries are added in the managed queue.
                //The VM is responsible for the actual growing/shrinking of 
                //threads. 

                EnsureVMInitialized(); 

#if FEATURE_LEGACY_THREADPOOL
                if (ThreadPoolGlobals.useNewWorkerPool)
                { 
#endif //FEATURE_LEGACY_THREADPOOL
                    // 
                    // If we are able to create the workitem, we need to get it in the queue without being interrupted 
                    // by a ThreadAbortException.
                    // 
                    try { }
                    finally
                    {
                        QueueUserWorkItemCallback tpcallBack = new QueueUserWorkItemCallback(callBack, state, compressStack, ref stackMark); 
                        ThreadPoolGlobals.workQueue.Enqueue(tpcallBack, true);
                        success = true; 
                    } 
#if FEATURE_LEGACY_THREADPOOL
                } 
                else
                {
                    _ThreadPoolWaitCallback tpcallBack = new _ThreadPoolWaitCallback(callBack, state, compressStack, ref stackMark);
 
                    int queueCount = ThreadPoolGlobals.tpQueue.EnQueue(tpcallBack);
 
                    //Make sure the unmanaged thread pool creates some threads 
                    //before accepting requests in the managed queue.
 
                    if ((ThreadPoolGlobals.tpHosted) || (queueCount < ThreadPoolGlobals.tpWarmupCount))
                    {
                        success = AdjustThreadsInPool((uint)ThreadPoolGlobals.tpQueue.GetQueueCount());
                    } 
                    else
                    { 
                        UpdateNativeTpCount((uint)ThreadPoolGlobals.tpQueue.GetQueueCount()); 
                    }
                } 
#endif //FEATURE_LEGACY_THREADPOOL
            }
            else
            { 
                throw new ArgumentNullException("WaitCallback");
            } 
            return success; 
        }
 
        [SecurityCritical]
        internal static void UnsafeQueueCustomWorkItem(IThreadPoolWorkItem workItem, bool forceGlobal)
        {
            Contract.Assert(null != workItem); 
            EnsureVMInitialized();
 
            // 
            // Enqueue needs to be protected from ThreadAbort
            // 
            try { }
            finally
            {
                ThreadPoolGlobals.workQueue.Enqueue(workItem, forceGlobal); 
            }
        } 
 
        // This method tries to take the target callback out of the current thread's queue.
        [SecurityCritical] 
        internal static bool TryPopCustomWorkItem(IThreadPoolWorkItem workItem)
        {
            Contract.Assert(null != workItem);
            if (!ThreadPoolGlobals.vmTpInitialized) 
                return false; //Not initialized, so there's no way this workitem was ever queued.
            return ThreadPoolGlobals.workQueue.LocalFindAndPop(workItem); 
        } 

        // Get all workitems.  Called by TaskScheduler in its debugger hooks. 
        [SecurityCritical]
        internal static IEnumerable<IThreadPoolWorkItem> GetQueuedWorkItems()
        {
            return EnumerateQueuedWorkItems(ThreadPoolWorkQueue.allThreadQueues.Current, ThreadPoolGlobals.workQueue.queueTail); 
        }
 
        internal static IEnumerable<IThreadPoolWorkItem> EnumerateQueuedWorkItems(ThreadPoolWorkQueue.WorkStealingQueue[] wsQueues, ThreadPoolWorkQueue.QueueSegment globalQueueTail) 
        {
#if FEATURE_LEGACY_THREADPOOL 
            Contract.Assert(ThreadPoolGlobals.useNewWorkerPool);
#endif //FEATURE_LEGACY_THREADPOOL

            if (wsQueues != null) 
            {
                // First, enumerate all workitems in thread-local queues. 
                foreach (ThreadPoolWorkQueue.WorkStealingQueue wsq in wsQueues) 
                {
                    if (wsq != null && wsq.m_array != null) 
                    {
                        IThreadPoolWorkItem[] items = wsq.m_array;
                        for (int i = 0; i < items.Length; i++)
                        { 
                            IThreadPoolWorkItem item = items[i];
                            if (item != null) 
                                yield return item; 
                        }
                    } 
                }
            }

            if (globalQueueTail != null) 
            {
                // Now the global queue 
                for (ThreadPoolWorkQueue.QueueSegment segment = globalQueueTail; 
                    segment != null;
                    segment = segment.Next) 
                {
                    IThreadPoolWorkItem[] items = segment.nodes;
                    for (int i = 0; i < items.Length; i++)
                    { 
                        IThreadPoolWorkItem item = items[i];
                        if (item != null) 
                            yield return item; 
                    }
                } 
            }
        }

        [SecurityCritical] 
        internal static IEnumerable<IThreadPoolWorkItem> GetLocallyQueuedWorkItems()
        { 
#if FEATURE_LEGACY_THREADPOOL 
            Contract.Assert(ThreadPoolGlobals.useNewWorkerPool);
#endif //FEATURE_LEGACY_THREADPOOL 

            return EnumerateQueuedWorkItems(new ThreadPoolWorkQueue.WorkStealingQueue[] { ThreadPoolWorkQueueThreadLocals.threadLocals.workStealingQueue }, null);
        }
 
        [SecurityCritical]
        internal static IEnumerable<IThreadPoolWorkItem> GetGloballyQueuedWorkItems() 
        { 
            return EnumerateQueuedWorkItems(null, ThreadPoolGlobals.workQueue.queueTail);
        } 

        private static object[] ToObjectArray(IEnumerable<IThreadPoolWorkItem> workitems)
        {
            int i = 0; 
            foreach (IThreadPoolWorkItem item in workitems)
            { 
                i++; 
            }
 
            object[] result = new object[i];
            i = 0;
            foreach (IThreadPoolWorkItem item in workitems)
            { 
                if (i < result.Length) //just in case someone calls us while the queues are in motion
                    result[i] = item; 
                i++; 
            }
 
            return result;
        }

        // This is the method the debugger will actually call, if it ends up calling 
        // into ThreadPool directly.  Tests can use this to simulate a debugger, as well.
        [SecurityCritical] 
        internal static object[] GetQueuedWorkItemsForDebugger() 
        {
            return ToObjectArray(GetQueuedWorkItems()); 
        }

        [SecurityCritical]
        internal static object[] GetGloballyQueuedWorkItemsForDebugger() 
        {
            return ToObjectArray(GetGloballyQueuedWorkItems()); 
        } 

        [SecurityCritical] 
        internal static object[] GetLocallyQueuedWorkItemsForDebugger()
        {
            return ToObjectArray(GetLocallyQueuedWorkItems());
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        internal static extern bool AdjustThreadsInPool(uint QueueLength);

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        unsafe private static extern bool PostQueuedCompletionStatus(NativeOverlapped* overlapped); 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [CLSCompliant(false)] 
        unsafe public static bool UnsafeQueueNativeOverlapped(NativeOverlapped* overlapped)
        {
#if FEATURE_CORECLR
            if(Environment.OSVersion.Platform == PlatformID.MacOSX) 
                throw new NotSupportedException(Environment.GetResourceString("Arg_NotSupportedException"));
            Contract.EndContractBlock(); 
#endif 

            return PostQueuedCompletionStatus(overlapped); 
        }

        [SecurityCritical]
        private static void EnsureVMInitialized() 
        {
            if (!ThreadPoolGlobals.vmTpInitialized) 
            { 
                ThreadPool.InitializeVMTp(ref ThreadPoolGlobals.enableWorkerTracking);
                ThreadPoolGlobals.vmTpInitialized = true; 
            }
        }

        // Native methods: 

#if FEATURE_LEGACY_THREADPOOL 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        internal static extern bool ShouldUseNewWorkerPool();
#endif //FEATURE_LEGACY_THREADPOOL
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern bool SetMinThreadsNative(int workerThreads, int completionPortThreads);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern bool SetMaxThreadsNative(int workerThreads, int completionPortThreads); 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void GetMinThreadsNative(out int workerThreads, out int completionPortThreads); 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void GetMaxThreadsNative(out int workerThreads, out int completionPortThreads);
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void GetAvailableThreadsNative(out int workerThreads, out int completionPortThreads);

#if FEATURE_LEGACY_THREADPOOL
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity] 
        internal static extern bool CompleteThreadPoolRequest(uint QueueLength);
#endif //FEATURE_LEGACY_THREADPOOL 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern bool NotifyWorkItemComplete();
 
        [System.Security.SecurityCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern void ReportThreadStatus(bool isWorking);

        [System.Security.SecuritySafeCritical]
        internal static void NotifyWorkItemProgress() 
        {
            if (!ThreadPoolGlobals.vmTpInitialized) 
                ThreadPool.InitializeVMTp(ref ThreadPoolGlobals.enableWorkerTracking); 
            NotifyWorkItemProgressNative();
        } 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern void NotifyWorkItemProgressNative();
 
#if FEATURE_LEGACY_THREADPOOL 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern bool ShouldReturnToVm();
#endif //FEATURE_LEGACY_THREADPOOL 

#if FEATURE_LEGACY_THREADPOOL 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern bool SetAppDomainRequestActive();
#endif //FEATURE_LEGACY_THREADPOOL 

#if FEATURE_LEGACY_THREADPOOL 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern void ClearAppDomainRequestActive();
#endif //FEATURE_LEGACY_THREADPOOL 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern bool IsThreadPoolHosted(); 

#if FEATURE_LEGACY_THREADPOOL
        [System.Security.SecurityCritical]  // auto-generated
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void SetNativeTpEvent(); 
#endif //FEATURE_LEGACY_THREADPOOL 

#if FEATURE_LEGACY_THREADPOOL 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void UpdateNativeTpCount(uint QueueLength); 
#endif //FEATURE_LEGACY_THREADPOOL
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        private static extern void InitializeVMTp(ref bool enableWorkerTracking);

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern IntPtr RegisterWaitForSingleObjectNative( 
             WaitHandle             waitHandle,
             Object                 state, 
             uint                   timeOutInterval,
             bool                   executeOnlyOnce,
             RegisteredWaitHandle   registeredWaitHandle,
             ref StackCrawlMark     stackMark, 
             bool                   compressStack
             ); 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [Obsolete("ThreadPool.BindHandle(IntPtr) has been deprecated.  Please use ThreadPool.BindHandle(SafeHandle) instead.", false)] 
        [SecurityPermissionAttribute( SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public static bool BindHandle(
             IntPtr osHandle
             ) 
        {
            return BindIOCompletionCallbackNative(osHandle); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [SecurityPermissionAttribute( SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public static bool BindHandle(SafeHandle osHandle)
        {
            #if FEATURE_CORECLR 
            if(Environment.OSVersion.Platform == PlatformID.MacOSX)
                throw new NotSupportedException(Environment.GetResourceString("Arg_NotSupportedException")); 
            Contract.EndContractBlock(); 
            #endif
 
            if (osHandle == null)
                throw new ArgumentNullException("osHandle");

            bool ret = false; 
            bool mustReleaseSafeHandle = false;
            RuntimeHelpers.PrepareConstrainedRegions(); 
            try { 
                osHandle.DangerousAddRef(ref mustReleaseSafeHandle);
                ret = BindIOCompletionCallbackNative(osHandle.DangerousGetHandle()); 
            }
            finally {
                if (mustReleaseSafeHandle)
                    osHandle.DangerousRelease(); 
            }
            return ret; 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private static extern bool BindIOCompletionCallbackNative(IntPtr fileHandle); 
    }
} 

