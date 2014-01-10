// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  ArraySortHelper 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: class to sort arrays
** 
**
===========================================================*/ 
namespace System.Collections.Generic 
{
    using System; 
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Diagnostics.Contracts;
 
    #region ArraySortHelper for single arrays
 
    [ContractClass(typeof(IArraySortHelperContract<>))] 
    internal interface IArraySortHelper<TKey>
    { 
        void Sort(TKey[] keys, int index, int length, IComparer<TKey> comparer);
        int BinarySearch(TKey[] keys, int index, int length, TKey value, IComparer<TKey> comparer);
    }
 
    [ContractClassFor(typeof(IArraySortHelper<>))]
    internal abstract class IArraySortHelperContract<TKey> : IArraySortHelper<TKey> 
    { 
        void IArraySortHelper<TKey>.Sort(TKey[] keys, int index, int length, IComparer<TKey> comparer)
        { 
            Contract.Requires(keys != null, "Check the arguments in the caller!");
            Contract.Requires(index >= 0 && index <= keys.Length);  // allow 0?
            Contract.Requires(length >= 0 && index + length <= keys.Length);
        } 

        int IArraySortHelper<TKey>.BinarySearch(TKey[] keys, int index, int length, TKey value, IComparer<TKey> comparer) 
        { 
            Contract.Requires(index >= 0 && index <= keys.Length);  // allow 0?
            Contract.Requires(length >= 0 && index + length <= keys.Length); 
            Contract.Ensures((Contract.Result<int>() >= index && Contract.Result<int>() <= index + length) ||
                (~Contract.Result<int>() >= index && ~Contract.Result<int>() <= index + length), "Binary search returned a bad value");

            return Contract.Result<int>(); 
        }
    } 
 
    [TypeDependencyAttribute("System.Collections.Generic.GenericArraySortHelper`1")]
    internal class ArraySortHelper<T> 
        : IArraySortHelper<T>
    {
        static IArraySortHelper<T> defaultArraySortHelper;
 
        public static IArraySortHelper<T> Default
        { 
            get 
            {
                IArraySortHelper<T> sorter = defaultArraySortHelper; 
                if (sorter == null)
                    sorter = CreateArraySortHelper();

                    return sorter; 
                }
            } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        private static IArraySortHelper<T> CreateArraySortHelper() 
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                defaultArraySortHelper = (IArraySortHelper<T>)RuntimeTypeHandle.Allocate(typeof(GenericArraySortHelper<string>).TypeHandle.Instantiate(new Type[] { typeof(T) })); 
            }
            else 
            { 
                defaultArraySortHelper = new ArraySortHelper<T>();
            } 
            return defaultArraySortHelper;
        }

        #region IArraySortHelper<T> Members 

        public void Sort(T[] keys, int index, int length, IComparer<T> comparer) 
        { 
            Contract.Assert(keys != null, "Check the arguments in the caller!");
            Contract.Assert( index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!"); 

            // Add a try block here to detect IComparers (or their
            // underlying IComparables, etc) that are bogus.
            try 
            {
                if (comparer == null) 
                { 
                comparer = Comparer<T>.Default;
            } 

                QuickSort(keys, index, index + (length - 1), comparer);
            }
            catch (IndexOutOfRangeException) 
            {
                throw new ArgumentException(Environment.GetResourceString("Arg_BogusIComparer", null, typeof(T).Name, comparer)); 
            } 
            catch (Exception e)
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_IComparerFailed"), e);
            }
        }
 
        public int BinarySearch(T[] array, int index, int length, T value, IComparer<T> comparer)
        { 
            try 
            {
                if (comparer == null) 
                {
                    comparer = Comparer<T>.Default;
                }
 
                return InternalBinarySearch(array, index, length, value, comparer);
            } 
            catch (Exception e) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_IComparerFailed"), e); 
            }
        }

        #endregion 

        internal static int InternalBinarySearch(T[] array, int index, int length, T value, IComparer<T> comparer) 
        { 
            Contract.Assert(array != null, "Check the arguments in the caller!");
            Contract.Assert(index >= 0 && length >= 0 && (array.Length - index >= length), "Check the arguments in the caller!"); 

            int lo = index;
            int hi = index + length - 1;
            while (lo <= hi) 
            {
                int i = lo + ((hi - lo) >> 1); 
                int order = comparer.Compare(array[i], value); 

                if (order == 0) return i; 
                if (order < 0)
                {
                    lo = i + 1;
                } 
                else
                { 
                    hi = i - 1; 
                }
        } 

            return ~lo;
        }
 
        private static void SwapIfGreaterWithItems(T[] keys, IComparer<T> comparer, int a, int b)
        { 
            if (a != b) 
            {
                if (comparer.Compare(keys[a], keys[b]) > 0) 
                {
                        T key = keys[a];
                        keys[a] = keys[b];
                        keys[b] = key; 
                }
            } 
        } 

        internal static void QuickSort(T[] keys, int left, int right, IComparer<T> comparer) 
        {
            do
            {
                int i = left; 
                int j = right;
 
                // pre-sort the low, middle (pivot), and high values in place. 
                // this improves performance in the face of already sorted data, or
                // data that is made up of multiple sorted runs appended together. 
                int middle = i + ((j - i) >> 1);
                SwapIfGreaterWithItems(keys, comparer, i, middle);  // swap the low with the mid point
                SwapIfGreaterWithItems(keys, comparer, i, j);   // swap the low with the high
                SwapIfGreaterWithItems(keys, comparer, middle, j); // swap the middle with the high 

                T x = keys[middle]; 
                do 
                {
                    while (comparer.Compare(keys[i], x) < 0) i++; 
                    while (comparer.Compare(x, keys[j]) < 0) j--;
                    Contract.Assert(i >= left && j <= right, "(i>=left && j<=right)  Sort failed - Is your IComparer bogus?");
                    if (i > j) break;
                    if (i < j) 
                    {
                        T key = keys[i]; 
                        keys[i] = keys[j]; 
                        keys[j] = key;
                    } 
                    i++;
                    j--;
                } while (i <= j);
                if (j - left <= right - i) 
                {
                    if (left < j) QuickSort(keys, left, j, comparer); 
                    left = i; 
                }
                else 
                {
                    if (i < right) QuickSort(keys, i, right, comparer);
                    right = j;
                } 
            } while (left < right);
        } 
    } 

    [Serializable()] 
    internal class GenericArraySortHelper<T>
        : IArraySortHelper<T>
        where T : IComparable<T>
    { 
        #region IArraySortHelper<T> Members
 
        public void Sort(T[] keys, int index, int length, IComparer<T> comparer) 
        {
            Contract.Assert(keys != null, "Check the arguments in the caller!"); 
            Contract.Assert(index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!");

            try
            { 
                if (comparer == null || comparer == Comparer<T>.Default)
                { 
                    // call the faster version of QuickSort if the user doesn't provide a comparer 
                    QuickSort(keys, index, index + (length - 1));
                } 
                else
                {
                    ArraySortHelper<T>.QuickSort(keys, index, index + (length - 1), comparer);
                } 
            }
            catch (IndexOutOfRangeException) 
            { 
                throw new ArgumentException(Environment.GetResourceString("Arg_BogusIComparer", default(T), typeof(T).Name, null));
            } 
            catch (Exception e)
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_IComparerFailed"), e);
            } 

                        } 
 
        public int BinarySearch(T[] array, int index, int length, T value, IComparer<T> comparer)
        { 
            Contract.Assert(array != null, "Check the arguments in the caller!");
            Contract.Assert(index >= 0 && length >= 0 && (array.Length - index >= length), "Check the arguments in the caller!");

            try 
            {
                if (comparer == null || comparer == Comparer<T>.Default) 
                { 
                    return BinarySearch(array, index, length, value);
                    } 
                else
                {
                    return ArraySortHelper<T>.InternalBinarySearch(array, index, length, value, comparer);
                } 
                }
            catch (Exception e) 
            { 
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_IComparerFailed"), e);
                } 
            }

        #endregion
 
        // This function is called when the user doesn't specify any comparer.
        // Since T is constrained here, we can call IComparable<T>.CompareTo here. 
        // We can avoid boxing for value type and casting for reference types. 
        private static int BinarySearch(T[] array, int index, int length, T value)
        { 
            int lo = index;
            int hi = index + length - 1;
            while (lo <= hi)
            { 
                int i = lo + ((hi - lo) >> 1);
                int order; 
                if (array[i] == null) 
                {
                    order = (value == null) ? 0 : -1; 
                }
                else
                {
                    order = array[i].CompareTo(value); 
                }
 
                if (order == 0) 
                {
                    return i; 
        }

                if (order < 0)
                { 
                    lo = i + 1;
                } 
                else 
                {
                    hi = i - 1; 
                }
            }

            return ~lo; 
        }
 
 
        private static void SwapIfGreaterWithItems(T[] keys, int a, int b)
        { 
            Contract.Requires(keys != null);
            Contract.Requires(0 <= a && a < keys.Length);
            Contract.Requires(0 <= b && b < keys.Length);
 
            if (a != b)
            { 
                if (keys[a] == null || keys[a].CompareTo(keys[b]) > 0) 
                {
                    T key = keys[a]; 
                    keys[a] = keys[b];
                    keys[b] = key;
                }
            } 
        }
 
        private static void QuickSort(T[] keys, int left, int right) 
        {
            Contract.Requires(keys != null); 
            Contract.Requires(0 <= left && left < keys.Length);
            Contract.Requires(0 <= right && right < keys.Length);

            // The code in this function looks very similar to QuickSort in ArraySortHelper<T> class. 
            // The difference is that T is constrainted to IComparable<T> here.
            // So the IL code will be different. This function is faster than the one in ArraySortHelper<T>. 
 
            do
            { 
                int i = left;
                int j = right;

                // pre-sort the low, middle (pivot), and high values in place. 
                // this improves performance in the face of already sorted data, or
                // data that is made up of multiple sorted runs appended together. 
                int middle = i + ((j - i) >> 1); 
                SwapIfGreaterWithItems(keys, i, middle); // swap the low with the mid point
                SwapIfGreaterWithItems(keys, i, j);      // swap the low with the high 
                SwapIfGreaterWithItems(keys, middle, j); // swap the middle with the high

                T x = keys[middle];
                do 
                {
                    if (x == null) 
                    { 
                        // if x null, the loop to find two elements to be switched can be reduced.
                        while (keys[j] != null) j--; 
                    }
                    else
                    {
                        while (x.CompareTo(keys[i]) > 0) i++; 
                        while (x.CompareTo(keys[j]) < 0) j--;
                    } 
                    Contract.Assert(i >= left && j <= right, "(i>=left && j<=right)  Sort failed - Is your IComparer bogus?"); 
                    if (i > j) break;
                    if (i < j) 
                    {
                        T key = keys[i];
                        keys[i] = keys[j];
                        keys[j] = key; 
                    }
                    i++; 
                    j--; 
                } while (i <= j);
                if (j - left <= right - i) 
                {
                    if (left < j) QuickSort(keys, left, j);
                    left = i;
                } 
                else
                { 
                    if (i < right) QuickSort(keys, i, right); 
                    right = j;
                } 
            } while (left < right);
        }
    }
 
    #endregion
 
    #region ArraySortHelper for paired key and value arrays 

    internal interface IArraySortHelper<TKey, TValue> 
    {
        void Sort(TKey[] keys, TValue[] values, int index, int length, IComparer<TKey> comparer);
    }
 
    [TypeDependencyAttribute("System.Collections.Generic.GenericArraySortHelper`2")]
    internal class ArraySortHelper<TKey, TValue> 
        : IArraySortHelper<TKey, TValue> 
    {
        static IArraySortHelper<TKey, TValue> defaultArraySortHelper; 

        public static IArraySortHelper<TKey, TValue> Default
        {
            get 
            {
                IArraySortHelper<TKey, TValue> sorter = defaultArraySortHelper; 
                if (sorter == null) 
                    sorter = CreateArraySortHelper();
 
                return sorter;
            }
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public static IArraySortHelper<TKey, TValue> CreateArraySortHelper() 
        { 
            if (typeof(IComparable<TKey>).IsAssignableFrom(typeof(TKey)))
            { 
                defaultArraySortHelper = (IArraySortHelper<TKey, TValue>)RuntimeTypeHandle.Allocate(typeof(GenericArraySortHelper<string, string>).TypeHandle.Instantiate(new Type[] { typeof(TKey), typeof(TValue) }));
            }
            else
            { 
                defaultArraySortHelper = new ArraySortHelper<TKey, TValue>();
            } 
            return defaultArraySortHelper; 
        }
 
        public void Sort(TKey[] keys, TValue[] values, int index, int length, IComparer<TKey> comparer)
        {
            Contract.Assert(keys != null, "Check the arguments in the caller!");  // Precondition on interface method
            Contract.Assert(index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!"); 

            // Add a try block here to detect IComparers (or their 
            // underlying IComparables, etc) that are bogus. 
            try
            { 
                if (comparer == null || comparer == Comparer<TKey>.Default)
                {
                    comparer = Comparer<TKey>.Default;
                } 

                QuickSort(keys, values, index, index + (length - 1), comparer); 
            } 
            catch (IndexOutOfRangeException)
            { 
                throw new ArgumentException(Environment.GetResourceString("Arg_BogusIComparer", null, typeof(TKey).Name, comparer));
            }
            catch (Exception e)
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_IComparerFailed"), e);
            } 
        } 

        private static void SwapIfGreaterWithItems(TKey[] keys, TValue[] values, IComparer<TKey> comparer, int a, int b) 
        {
            Contract.Requires(keys != null);
            Contract.Requires(values == null || values.Length >= keys.Length);
            Contract.Requires(comparer != null); 
            Contract.Requires(0 <= a && a < keys.Length);
            Contract.Requires(0 <= b && b < keys.Length); 
 
            if (a != b)
            { 
                if (a != b && comparer.Compare(keys[a], keys[b]) > 0)
                {
                    TKey key = keys[a];
                    keys[a] = keys[b]; 
                    keys[b] = key;
                    if (values != null) 
                    { 
                        TValue value = values[a];
                        values[a] = values[b]; 
                        values[b] = value;
                    }
                }
            } 
        }
 
        internal static void QuickSort(TKey[] keys, TValue[] values, int left, int right, IComparer<TKey> comparer) 
        {
            do 
            {
                int i = left;
                int j = right;
 
                // pre-sort the low, middle (pivot), and high values in place.
                // this improves performance in the face of already sorted data, or 
                // data that is made up of multiple sorted runs appended together. 
                int middle = i + ((j - i) >> 1);
                SwapIfGreaterWithItems(keys, values, comparer, i, middle);  // swap the low with the mid point 
                SwapIfGreaterWithItems(keys, values, comparer, i, j);   // swap the low with the high
                SwapIfGreaterWithItems(keys, values, comparer, middle, j); // swap the middle with the high

                TKey x = keys[middle]; 
                do
                { 
                        while (comparer.Compare(keys[i], x) < 0) i++; 
                        while (comparer.Compare(x, keys[j]) < 0) j--;
                    Contract.Assert(i>=left && j<=right, "(i>=left && j<=right)  Sort failed - Is your IComparer bogus?"); 
                    if (i > j) break;
                    if (i < j)
                    {
                        TKey key = keys[i]; 
                        keys[i] = keys[j];
                        keys[j] = key; 
                        if (values != null) 
                        {
                            TValue value = values[i]; 
                            values[i] = values[j];
                            values[j] = value;
                        }
                    } 
                    i++;
                    j--; 
                } while (i <= j); 
                if (j - left <= right - i)
                { 
                    if (left < j) QuickSort(keys, values, left, j, comparer);
                    left = i;
                }
                else 
                {
                    if (i < right) QuickSort(keys, values, i, right, comparer); 
                    right = j; 
                }
            } while (left < right); 
        }
            }

    internal class GenericArraySortHelper<TKey, TValue> 
        : IArraySortHelper<TKey, TValue>
        where TKey : IComparable<TKey> 
    { 
        public void Sort(TKey[] keys, TValue[] values, int index, int length, IComparer<TKey> comparer)
        { 
            Contract.Assert(keys != null, "Check the arguments in the caller!");
            Contract.Assert( index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!");

            // Add a try block here to detect IComparers (or their 
            // underlying IComparables, etc) that are bogus.
            try 
            { 
                if (comparer == null || comparer == Comparer<TKey>.Default)
                { 
                    // call the faster version of QuickSort if the user doesn't provide a comparer
                QuickSort(keys, values, index, index + length -1);
            }
                else 
                {
                    ArraySortHelper<TKey, TValue>.QuickSort(keys, values, index, index + length - 1, comparer); 
            } 

                    } 
            catch (IndexOutOfRangeException)
            {
                throw new ArgumentException(Environment.GetResourceString("Arg_BogusIComparer", null, typeof(TKey).Name, null));
                } 
            catch (Exception e)
            { 
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_IComparerFailed"), e); 
                }
        } 

        private static void SwapIfGreaterWithItems(TKey[] keys, TValue[] values, int a, int b)
        {
            if (a != b) 
            {
                if (keys[a] == null || keys[a].CompareTo(keys[b]) > 0) 
                { 
                    TKey key = keys[a];
                        keys[a] = keys[b]; 
                        keys[b] = key;
                    if (values != null)
                    {
                            TValue value = values[a]; 
                            values[a] = values[b];
                            values[b] = value; 
                        } 
                    }
                } 
        }

        private static void QuickSort(TKey[] keys, TValue[] values, int left, int right)
        { 
             // The code in this function looks very similar to QuickSort in ArraySortHelper<T> class.
             // The difference is that T is constrainted to IComparable<T> here. 
             // So the IL code will be different. This function is faster than the one in ArraySortHelper<T>. 

            do 
            {
                int i = left;
                int j = right;
 
                // pre-sort the low, middle (pivot), and high values in place.
                // this improves performance in the face of already sorted data, or 
                // data that is made up of multiple sorted runs appended together. 
                int middle = i + ((j - i) >> 1);
                SwapIfGreaterWithItems(keys, values, i, middle); // swap the low with the mid point 
                SwapIfGreaterWithItems(keys, values, i, j);      // swap the low with the high
                SwapIfGreaterWithItems(keys, values, middle, j); // swap the middle with the high

                TKey x = keys[middle]; 
                do
                { 
                    if (x == null) 
                    {
                            // if x null, the loop to find two elements to be switched can be reduced. 
                            while (keys[j] != null) j--;
                        }
                    else
                    { 
                            while(x.CompareTo(keys[i]) > 0) i++;
                            while(x.CompareTo(keys[j]) < 0) j--; 
                        } 
                    Contract.Assert(i>=left && j<=right, "(i>=left && j<=right)  Sort failed - Is your IComparer bogus?");
                    if (i > j) break; 
                    if (i < j)
                    {
                        TKey key = keys[i];
                        keys[i] = keys[j]; 
                        keys[j] = key;
                        if (values != null) 
                        { 
                            TValue value = values[i];
                            values[i] = values[j]; 
                            values[j] = value;
                        }
                    }
                    i++; 
                    j--;
                } while (i <= j); 
                if (j - left <= right - i) 
                {
                    if (left < j) QuickSort(keys, values, left, j); 
                    left = i;
                }
                else
                { 
                    if (i < right) QuickSort(keys, values, i, right);
                    right = j; 
                } 
            } while (left < right);
        } 
    }

    #endregion
} 

 

