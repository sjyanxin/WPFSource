// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 

namespace System { 
    using System.Collections; 
    using System.Collections.Generic;
    using System.Globalization; 
    using System.Diagnostics.Contracts;

    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)] 
    public abstract class StringComparer : IComparer, IEqualityComparer, IComparer<string>, IEqualityComparer<string>{
        private static readonly StringComparer _invariantCulture = new CultureAwareComparer(CultureInfo.InvariantCulture, false); 
        private static readonly StringComparer _invariantCultureIgnoreCase = new CultureAwareComparer(CultureInfo.InvariantCulture, true); 
        private static readonly StringComparer _ordinal = new OrdinalComparer(false);
        private static readonly StringComparer _ordinalIgnoreCase = new OrdinalComparer(true); 

        public static StringComparer InvariantCulture {
            get {
                Contract.Ensures(Contract.Result<StringComparer>() != null); 
                return _invariantCulture;
            } 
        } 

        public static StringComparer InvariantCultureIgnoreCase { 
            get {
                Contract.Ensures(Contract.Result<StringComparer>() != null);
                return _invariantCultureIgnoreCase;
            } 
        }
 
        public static StringComparer CurrentCulture { 
            get {
                Contract.Ensures(Contract.Result<StringComparer>() != null); 
                return new CultureAwareComparer(CultureInfo.CurrentCulture, false);
            }
        }
 
        public static StringComparer CurrentCultureIgnoreCase {
            get { 
                Contract.Ensures(Contract.Result<StringComparer>() != null); 
                return new CultureAwareComparer(CultureInfo.CurrentCulture, true);
            } 
        }

        public static StringComparer Ordinal {
            get { 
                Contract.Ensures(Contract.Result<StringComparer>() != null);
                return _ordinal; 
            } 
        }
 
        public static StringComparer OrdinalIgnoreCase {
            get {
                Contract.Ensures(Contract.Result<StringComparer>() != null);
                return _ordinalIgnoreCase; 
            }
        } 
 
        public static StringComparer Create(CultureInfo culture, bool ignoreCase) {
            if( culture == null) { 
                throw new ArgumentNullException("culture");
            }
            Contract.Ensures(Contract.Result<StringComparer>() != null);
            Contract.EndContractBlock(); 

            return new CultureAwareComparer(culture, ignoreCase); 
        } 

        public int Compare(object x, object y) { 
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
 
            String sa = x as String;
            if (sa != null) { 
                String sb = y as String; 
                if( sb != null) {
                    return Compare(sa, sb); 
                }
            }

            IComparable ia = x as IComparable; 
            if (ia != null) {
                return ia.CompareTo(y); 
            } 

            throw new ArgumentException(Environment.GetResourceString("Argument_ImplementIComparable")); 
        }


        public new bool Equals(Object x, Object y) { 
            if (x == y) return true;
            if (x == null || y == null) return false; 
 
            String sa = x as String;
            if (sa != null) { 
                String sb = y as String;
                if( sb != null) {
                    return Equals(sa, sb);
                } 
            }
            return x.Equals(y); 
        } 

        public int GetHashCode(object obj) { 
            if( obj == null) {
                throw new ArgumentNullException("obj");
            }
            Contract.EndContractBlock(); 

            string s = obj as string; 
            if( s != null) { 
                return GetHashCode(s);
            } 
            return obj.GetHashCode();
        }

        public abstract int Compare(String x, String y); 
        public abstract bool Equals(String x, String y);
        public abstract int GetHashCode(string obj); 
    } 

    [Serializable] 
    internal sealed class CultureAwareComparer : StringComparer {
         private CompareInfo _compareInfo;
         private bool            _ignoreCase;
 
        internal CultureAwareComparer(CultureInfo culture, bool ignoreCase) {
               _compareInfo = culture.CompareInfo; 
               _ignoreCase = ignoreCase; 
        }
 
        public override int Compare(string x, string y) {
            if (Object.ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1; 
            return _compareInfo.Compare(x, y, _ignoreCase? CompareOptions.IgnoreCase :  CompareOptions.None);
        } 
 
        public override bool Equals(string x, string y) {
            if (Object.ReferenceEquals(x ,y)) return true; 
            if (x == null || y == null) return false;

            return (_compareInfo.Compare(x, y, _ignoreCase? CompareOptions.IgnoreCase :  CompareOptions.None) == 0);
        } 

        public override int GetHashCode(string obj) { 
            if( obj == null) { 
                throw new ArgumentNullException("obj");
            } 
            Contract.EndContractBlock();

            if( _ignoreCase) {
                return _compareInfo.GetHashCodeOfString(obj, CompareOptions.IgnoreCase); 
            }
 
            return _compareInfo.GetHashCodeOfString(obj, CompareOptions.None); 
        }
 
        // Equals method for the comparer itself.
        public override bool Equals(Object obj){
            CultureAwareComparer comparer = obj as CultureAwareComparer;
            if( comparer == null) { 
                return false;
            } 
            return (this._ignoreCase == comparer._ignoreCase) && ( this._compareInfo.Equals(comparer._compareInfo)); 
        }
 
        public override int GetHashCode() {
            int hashCode = _compareInfo.GetHashCode() ;
            return _ignoreCase ? (~hashCode) : hashCode;
        } 
    }
 
 
    // Provide x more optimal implementation of ordinal comparison.
    [Serializable] 
    internal sealed class OrdinalComparer : StringComparer  {
        private bool            _ignoreCase;

        internal OrdinalComparer(bool ignoreCase) { 
               _ignoreCase = ignoreCase;
        } 
 
        public override int Compare(string x, string y) {
            if (Object.ReferenceEquals(x, y)) return 0; 
            if (x == null) return -1;
            if (y == null) return 1;

            if( _ignoreCase) { 
                return String.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            } 
 
            return String.CompareOrdinal(x, y);
        } 

        public override bool Equals(string x, string y) {
            if (Object.ReferenceEquals(x ,y)) return true;
            if (x == null || y == null) return false; 

            if( _ignoreCase) { 
                if( x.Length != y.Length) { 
                    return false;
                } 
                return (String.Compare(x, y, StringComparison.OrdinalIgnoreCase) == 0);
            }
            return x.Equals(y);
        } 

        public override int GetHashCode(string obj) { 
            if( obj == null) { 
                throw new ArgumentNullException("obj");
            } 
            Contract.EndContractBlock();

            if( _ignoreCase) {
                return TextInfo.GetHashCodeOrdinalIgnoreCase(obj); 
            }
 
            return obj.GetHashCode(); 
        }
 
        // Equals method for the comparer itself.
        public override bool Equals(Object obj){
            OrdinalComparer comparer = obj as OrdinalComparer;
            if( comparer == null) { 
                return false;
            } 
            return (this._ignoreCase == comparer._ignoreCase); 
        }
 
        public override int GetHashCode() {
            string name = "OrdinalComparer";
            int hashCode = name.GetHashCode();
            return _ignoreCase ? (~hashCode) : hashCode; 
        }
    } 
} 

