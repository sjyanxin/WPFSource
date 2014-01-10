// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
using System.Reflection;
using System.Text; 
using System.Collections; 
using System.Globalization;
using System.Runtime.CompilerServices; 
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts; 

namespace System 
{ 
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public abstract class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        #region Private Static Data Members
        private static readonly char [] enumSeperatorCharArray = new char [] {','}; 
        private const String enumSeperator = ", ";
        private static Hashtable fieldInfoHash = Hashtable.Synchronized(new Hashtable()); 
        private const int maxHashElements = 100; // to trim the working set 
        #endregion
 
        #region Private Static Methods
        [System.Security.SecuritySafeCritical]  // auto-generated
        private static HashEntry GetHashEntry(RuntimeType enumType)
        { 
            Contract.Requires(enumType != null);
            Contract.Ensures(Contract.Result<HashEntry>() != null); 
 
            HashEntry hashEntry = (HashEntry)fieldInfoHash[enumType];
 
            if (hashEntry == null)
            {
                // To reduce the workingset we clear the hashtable when a threshold number of elements are inserted.
                if (fieldInfoHash.Count > maxHashElements) 
                    fieldInfoHash.Clear();
 
                ulong[] values = null; 
                String[] names = null;
 
                GetEnumValues(enumType.GetTypeHandleInternal(),
                    JitHelpers.GetObjectHandleOnStack(ref values), JitHelpers.GetObjectHandleOnStack(ref names));

                hashEntry = new HashEntry(names, values); 
                fieldInfoHash[enumType] = hashEntry;
            } 
 
            return hashEntry;
        } 

        private static String InternalFormattedHexString(Object value)
        {
            TypeCode typeCode = Convert.GetTypeCode(value); 

            switch (typeCode) 
            { 
                case TypeCode.SByte :
                    { 
                        Byte result = (byte)(sbyte)value;

                        return result.ToString("X2", null);
                    } 

                case TypeCode.Byte : 
                    { 
                        Byte result = (byte)value;
 
                        return result.ToString("X2", null);
                    }

                case TypeCode.Int16 : 
                    {
                        UInt16 result = (UInt16)(Int16)value; 
 
                        return result.ToString("X4", null);
                    } 

                case TypeCode.UInt16 :
                    {
                        UInt16 result = (UInt16)value; 

                        return result.ToString("X4", null); 
                    } 

                case TypeCode.UInt32 : 
                    {
                        UInt32 result = (UInt32)value;

                        return result.ToString("X8", null); 
                    }
 
                case TypeCode.Int32 : 
                    {
                        UInt32 result = (UInt32)(int)value; 

                        return result.ToString("X8", null);
                    }
 
                case TypeCode.UInt64 :
                    { 
                        UInt64 result = (UInt64)value; 

                        return result.ToString("X16", null); 
                    }

                case TypeCode.Int64 :
                    { 
                        UInt64 result = (UInt64)(Int64)value;
 
                        return result.ToString("X16", null); 
                    }
 
                // All unsigned types will be directly cast
                default :
                    Contract.Assert(false, "Invalid Object type in Format");
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_UnknownEnumType")); 
            }
        } 
 
        private static String InternalFormat(RuntimeType eT, Object value)
        { 
            Contract.Requires(eT != null);
            Contract.Requires(value != null);
            if (!eT.IsDefined(typeof(System.FlagsAttribute), false)) // Not marked with Flags attribute
            { 
                // Try to see if its one of the enum values, then we return a String back else the value
                String retval = GetName(eT, value); 
                if (retval == null) 
                    return value.ToString();
                else 
                    return retval;
            }
            else // These are flags OR'ed together (We treat everything as unsigned types)
            { 
                return InternalFlagsFormat(eT, value);
 
            } 
        }
 
        private static String InternalFlagsFormat(RuntimeType eT, Object value)
        {
            Contract.Requires(eT != null);
            Contract.Requires(value != null); 
            ulong result = ToUInt64(value);
            HashEntry hashEntry = GetHashEntry(eT); 
            // These values are sorted by value. Don't change this 
            String[] names = hashEntry.names;
            ulong[] values = hashEntry.values; 
            Contract.Assert(names.Length == values.Length);

            int index = values.Length - 1;
            StringBuilder retval = new StringBuilder(); 
            bool firstTime = true;
            ulong saveResult = result; 
 
            // We will not optimize this code further to keep it maintainable. There are some boundary checks that can be applied
            // to minimize the comparsions required. This code works the same for the best/worst case. In general the number of 
            // items in an enum are sufficiently small and not worth the optimization.
            while (index >= 0)
            {
                if ((index == 0) && (values[index] == 0)) 
                    break;
 
                if ((result & values[index]) == values[index]) 
                {
                    result -= values[index]; 
                    if (!firstTime)
                        retval.Insert(0, enumSeperator);

                    retval.Insert(0, names[index]); 
                    firstTime = false;
                } 
 
                index--;
            } 

            // We were unable to represent this number as a bitwise or of valid flags
            if (result != 0)
                return value.ToString(); 

            // For the case when we have zero 
            if (saveResult==0) 
            {
                if (values.Length > 0 && values[0] == 0) 
                    return names[0]; // Zero was one of the enum values.
                else
                    return "0";
            } 
            else
            return retval.ToString(); // Return the string representation 
        } 

        internal static ulong ToUInt64(Object value) 
        {
            // Helper function to silently convert the value to UInt64 from the other base types for enum without throwing an exception.
            // This is need since the Convert functions do overflow checks.
            TypeCode typeCode = Convert.GetTypeCode(value); 
            ulong result;
 
            switch(typeCode) 
            {
                case TypeCode.SByte: 
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    result = (UInt64)Convert.ToInt64(value, CultureInfo.InvariantCulture); 
                    break;
 
                case TypeCode.Byte: 
                case TypeCode.UInt16:
                case TypeCode.UInt32: 
                case TypeCode.UInt64:
                    result = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                    break;
 
                default:
                // All unsigned types will be directly cast 
                    Contract.Assert(false, "Invalid Object type in ToUInt64"); 
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_UnknownEnumType"));
            } 
            return result;
        }

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern int InternalCompareTo(Object o1, Object o2); 

        [System.Security.SecuritySafeCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern RuntimeType InternalGetUnderlyingType(RuntimeType enumType);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        private static extern void GetEnumValues(RuntimeTypeHandle enumType, ObjectHandleOnStack values, ObjectHandleOnStack names); 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern Object InternalBoxEnum(RuntimeType enumType, long value);
        #endregion 
 
        #region Public Static Methods
        private enum ParseFailureKind 
        {
            None                  = 0,
            Argument              = 1,
            ArgumentNull          = 2, 
            ArgumentWithParameter = 3,
            UnhandledException    = 4 
        } 

        // This will store the result of the parsing. 
        private struct EnumResult
        {
            internal object parsedEnum;
            internal bool canThrow; 
            internal ParseFailureKind m_failure;
            internal string m_failureMessageID; 
            internal string m_failureParameter; 
            internal object m_failureMessageFormatArgument;
            internal Exception m_innerException; 

            internal void Init(bool canMethodThrow)
            {
                parsedEnum = 0; 
                canThrow = canMethodThrow;
            } 
            internal void SetFailure(Exception unhandledException) 
            {
                m_failure = ParseFailureKind.UnhandledException; 
                m_innerException = unhandledException;
            }
            internal void SetFailure(ParseFailureKind failure, string failureParameter)
            { 
                m_failure = failure;
                m_failureParameter = failureParameter; 
                if (canThrow) 
                    throw GetEnumParseException();
            } 
            internal void SetFailure(ParseFailureKind failure, string failureMessageID, object failureMessageFormatArgument)
            {
                m_failure = failure;
                m_failureMessageID = failureMessageID; 
                m_failureMessageFormatArgument = failureMessageFormatArgument;
                if (canThrow) 
                    throw GetEnumParseException(); 
            }
            internal Exception GetEnumParseException() 
            {
                switch (m_failure)
                {
                    case ParseFailureKind.Argument: 
                        return new ArgumentException(Environment.GetResourceString(m_failureMessageID));
 
                    case ParseFailureKind.ArgumentNull: 
                        return new ArgumentNullException(m_failureParameter);
 
                    case ParseFailureKind.ArgumentWithParameter:
                        return new ArgumentException(Environment.GetResourceString(m_failureMessageID, m_failureMessageFormatArgument));

                    case ParseFailureKind.UnhandledException: 
                        return m_innerException;
 
                    default: 
                        Contract.Assert(false, "Unknown EnumParseFailure: " + m_failure);
                        return new ArgumentException(Environment.GetResourceString("Arg_EnumValueNotFound")); 
                }
            }
        }
 
        [System.Security.SecuritySafeCritical]
        public static bool TryParse<TEnum>(String value, out TEnum result) where TEnum : struct 
        { 
            return TryParse(value, false, out result);
        } 

        [System.Security.SecuritySafeCritical]
        public static bool TryParse<TEnum>(String value, bool ignoreCase, out TEnum result) where TEnum : struct
        { 
            result = default(TEnum);
            EnumResult parseResult = new EnumResult(); 
            parseResult.Init(false); 
            bool retValue;
 
            if (retValue = TryParseEnum(typeof(TEnum), value, ignoreCase, ref parseResult))
                result = (TEnum)parseResult.parsedEnum;
            return retValue;
        } 

        [System.Runtime.InteropServices.ComVisible(true)] 
        public static Object Parse(Type enumType, String value) 
        {
            return Parse(enumType, value, false); 
        }

        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object Parse(Type enumType, String value, bool ignoreCase) 
        {
            EnumResult parseResult = new EnumResult(); 
            parseResult.Init(true); 
            if (TryParseEnum(enumType, value, ignoreCase, ref parseResult))
                return parseResult.parsedEnum; 
            else
                throw parseResult.GetEnumParseException();
        }
 
        [System.Security.SecuritySafeCritical]
        private static bool TryParseEnum(Type enumType, String value, bool ignoreCase, ref EnumResult parseResult) 
        { 
            if (enumType == null)
                throw new ArgumentNullException("enumType"); 
            Contract.EndContractBlock();

            RuntimeType rtType = enumType as RuntimeType;
            if (rtType == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType");
 
            if (!enumType.IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
 
            if (value == null) {
                parseResult.SetFailure(ParseFailureKind.ArgumentNull, "value");
                return false;
            } 

            value = value.Trim(); 
            if (value.Length == 0) { 
                parseResult.SetFailure(ParseFailureKind.Argument, "Arg_MustContainEnumInfo", null);
                return false; 
            }

            // We have 2 code paths here. One if they are values else if they are Strings.
            // values will have the first character as as number or a sign. 
            ulong result = 0;
 
            if (Char.IsDigit(value[0]) || value[0] == '-' || value[0] == '+') 
            {
                Type underlyingType = GetUnderlyingType(enumType); 
                Object temp;

                try
                { 
                    temp = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                    parseResult.parsedEnum = ToObject(enumType, temp); 
                    return true; 
                }
                catch (FormatException) 
                { // We need to Parse this as a String instead. There are cases
                  // when you tlbimp enums that can have values of the form "3D".
                  // Don't fix this code.
                } 
                catch (Exception ex)
                { 
                    if (parseResult.canThrow) 
                        throw;
                    else 
                    {
                        parseResult.SetFailure(ex);
                        return false;
                    } 
                }
            } 
 
            String[] values = value.Split(enumSeperatorCharArray);
 
            // Find the field.Lets assume that these are always static classes because the class is
            //  an enum.
            HashEntry hashEntry = GetHashEntry(rtType);
            String[] names = hashEntry.names; 

            for (int i = 0; i < values.Length; i++) 
            { 
                values[i] = values[i].Trim(); // We need to remove whitespace characters
 
                bool success = false;

                for (int j = 0; j < names.Length; j++)
                { 
                    if (ignoreCase)
                    { 
                        if (String.Compare(names[j], values[i], StringComparison.OrdinalIgnoreCase) != 0) 
                            continue;
                    } 
                    else
                    {
                        if (!names[j].Equals(values[i]))
                            continue; 
                    }
 
                    ulong item = hashEntry.values[j]; 

                    result |= item; 
                    success = true;
                    break;
                }
 
                if (!success)
                { 
                    // Not found, throw an argument exception. 
                    parseResult.SetFailure(ParseFailureKind.ArgumentWithParameter, "Arg_EnumValueNotFound", value);
                    return false; 
                }
            }

            try 
            {
                parseResult.parsedEnum = ToObject(enumType, result); 
                return true; 
            }
            catch (Exception ex) 
            {
                if (parseResult.canThrow)
                    throw;
                else 
                {
                    parseResult.SetFailure(ex); 
                    return false; 
                }
            } 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(true)] 
        public static Type GetUnderlyingType(Type enumType)
        { 
            if (enumType == null) 
                throw new ArgumentNullException("enumType");
            Contract.EndContractBlock(); 

            return enumType.GetEnumUnderlyingType();
        }
 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Array GetValues(Type enumType) 
        { 
            if (enumType == null)
                throw new ArgumentNullException("enumType"); 
            Contract.EndContractBlock();

            return enumType.GetEnumValues();
        } 

        internal static ulong[] InternalGetValues(RuntimeType enumType) 
        { 
            // Get all of the values
            return GetHashEntry(enumType).values; 
        }

        [System.Runtime.InteropServices.ComVisible(true)]
        public static String GetName(Type enumType, Object value) 
        {
            if (enumType == null) 
                throw new ArgumentNullException("enumType"); 
            Contract.EndContractBlock();
 
            return enumType.GetEnumName(value);
        }

        [System.Runtime.InteropServices.ComVisible(true)] 
        public static String[] GetNames(Type enumType)
        { 
            if (enumType == null) 
                throw new ArgumentNullException("enumType");
            Contract.EndContractBlock(); 

            return enumType.GetEnumNames();
        }
 
        internal static String[] InternalGetNames(RuntimeType enumType)
        { 
            return GetHashEntry(enumType).names; 
        }
 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, Object value)
        {
            if (value == null) 
                throw new ArgumentNullException("value");
            Contract.EndContractBlock(); 
 
            // Delegate rest of error checking to the other functions
            TypeCode typeCode = Convert.GetTypeCode(value); 

            switch (typeCode)
            {
                case TypeCode.Int32 : 
                    return ToObject(enumType, (int)value);
 
                case TypeCode.SByte : 
                    return ToObject(enumType, (sbyte)value);
 
                case TypeCode.Int16 :
                    return ToObject(enumType, (short)value);

                case TypeCode.Int64 : 
                    return ToObject(enumType, (long)value);
 
                case TypeCode.UInt32 : 
                    return ToObject(enumType, (uint)value);
 
                case TypeCode.Byte :
                    return ToObject(enumType, (byte)value);

                case TypeCode.UInt16 : 
                    return ToObject(enumType, (ushort)value);
 
                case TypeCode.UInt64 : 
                    return ToObject(enumType, (ulong)value);
 
                default :
                    // All unsigned types will be directly cast
                    throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnumBaseTypeOrEnum"), "value");
            } 
        }
 
        [Pure] 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static bool IsDefined(Type enumType, Object value) 
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            Contract.EndContractBlock(); 

            return enumType.IsEnumDefined(value); 
        } 

        [System.Runtime.InteropServices.ComVisible(true)] 
        public static String Format(Type enumType, Object value, String format)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType"); 

            if (!enumType.IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType"); 

            if (value == null) 
                throw new ArgumentNullException("value");

            if (format == null)
                throw new ArgumentNullException("format"); 
            Contract.EndContractBlock();
 
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType"); 

            // Check if both of them are of the same type
            Type valueType = value.GetType();
 
            Type underlyingType = GetUnderlyingType(enumType);
 
            // If the value is an Enum then we need to extract the underlying value from it 
            if (valueType.IsEnum) {
                Type valueUnderlyingType = GetUnderlyingType(valueType); 

                if (!valueType.IsEquivalentTo(enumType))
                    throw new ArgumentException(Environment.GetResourceString("Arg_EnumAndObjectMustBeSameType", valueType.ToString(), enumType.ToString()));
 
                valueType = valueUnderlyingType;
                value = ((Enum)value).GetValue(); 
            } 
            // The value must be of the same type as the Underlying type of the Enum
            else if (valueType != underlyingType) { 
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumFormatUnderlyingTypeAndObjectMustBeSameType", valueType.ToString(), underlyingType.ToString()));
            }

            if( format.Length != 1) { 
                // all acceptable format string are of length 1
                throw new FormatException(Environment.GetResourceString("Format_InvalidEnumFormatSpecification")); 
            } 

            char formatCh = format[0]; 

            if (formatCh == 'D' || formatCh == 'd') {
                return value.ToString();
            } 

            if (formatCh == 'X' || formatCh == 'x') { 
                // Retrieve the value from the field. 
                return InternalFormattedHexString(value);
            } 

            if (formatCh == 'G' || formatCh == 'g') {
                return InternalFormat(rtType, value);
            } 

            if (formatCh == 'F' || formatCh == 'f') { 
                return InternalFlagsFormat(rtType, value); 
            }
 
            throw new FormatException(Environment.GetResourceString("Format_InvalidEnumFormatSpecification"));
        }

        #endregion 

        #region Definitions 
        private class HashEntry 
        {
            // Each entry contains a list of sorted pair of enum field names and values, sorted by values 
            public HashEntry(String [] names, ulong [] values)
            {
                this.names = names;
                this.values = values; 
            }
 
            public String[] names; 
            public ulong [] values;
        } 
        #endregion

        #region Private Methods
        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal Object GetValue()
        { 
            return InternalGetValue(); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern Object InternalGetValue(); 

        #endregion 
 
        #region Object Overrides
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern override bool Equals(Object obj);
 
        public override int GetHashCode()
        { 
            return GetValue().GetHashCode(); 
        }
 
        public override String ToString()
        {
            // Returns the value in a human readable format.  For PASCAL style enums who's value maps directly the name of the field is returned.
            // For PASCAL style enums who's values do not map directly the decimal value of the field is returned. 
            // For BitFlags (indicated by the Flags custom attribute): If for each bit that is set in the value there is a corresponding constant
            //(a pure power of 2), then the  OR string (ie "Red | Yellow") is returned. Otherwise, if the value is zero or if you can't create a string that consists of 
            // pure powers of 2 OR-ed together, you return a hex value 
            return Enum.InternalFormat((RuntimeType)GetType(), GetValue());
        } 
        #endregion

        #region IFormattable
        [Obsolete("The provider argument is not used. Please use ToString(String).")] 
        public String ToString(String format, IFormatProvider provider)
        { 
            return ToString(format); 
        }
        #endregion 

        #region IComparable
        [System.Security.SecuritySafeCritical]  // auto-generated
        public int CompareTo(Object target) 
        {
            const int retIncompatibleMethodTables = 2;  // indicates that the method tables did not match 
            const int retInvalidEnumType = 3; // indicates that the enum was of an unknown/unsupported unerlying type 

            if (this == null) 
                throw new NullReferenceException();
            Contract.EndContractBlock();

            int ret = InternalCompareTo(this, target); 

            if (ret < retIncompatibleMethodTables) 
            { 
                // -1, 0 and 1 are the normal return codes
                return ret; 
            }
            else if (ret == retIncompatibleMethodTables)
            {
                Type thisType = this.GetType(); 
                Type targetType = target.GetType();
 
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumAndObjectMustBeSameType", 
                        targetType.ToString(), thisType.ToString()));
            } 
            else
            {
                // assert valid return code (3)
                Contract.Assert(ret == retInvalidEnumType, "Enum.InternalCompareTo return code was invalid"); 

                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_UnknownEnumType")); 
            } 
        }
        #endregion 

        #region Public Methods
        [System.Security.SecuritySafeCritical]  // auto-generated
        public String ToString(String format) { 
            if (format == null || format.Length == 0)
                format = "G"; 
 
            if (String.Compare(format, "G", StringComparison.OrdinalIgnoreCase) == 0)
                return ToString(); 

            if (String.Compare(format, "D", StringComparison.OrdinalIgnoreCase) == 0)
                return GetValue().ToString();
 
            if (String.Compare(format, "X", StringComparison.OrdinalIgnoreCase) == 0)
                return InternalFormattedHexString(GetValue()); 
 
            if (String.Compare(format, "F", StringComparison.OrdinalIgnoreCase) == 0)
                return InternalFlagsFormat((RuntimeType)GetType(), GetValue()); 

            throw new FormatException(Environment.GetResourceString("Format_InvalidEnumFormatSpecification"));
        }
 
        [Obsolete("The provider argument is not used. Please use ToString().")]
        public String ToString(IFormatProvider provider) 
        { 
            return ToString();
        } 

        public Boolean HasFlag(Enum flag) {
            if (!this.GetType().IsEquivalentTo(flag.GetType())) {
                throw new ArgumentException(Environment.GetResourceString("Argument_EnumTypeDoesNotMatch", flag.GetType(), this.GetType())); 
            }
 
            ulong uFlag = ToUInt64(flag.GetValue()); 
            ulong uThis = ToUInt64(GetValue());
            return ((uThis & uFlag) == uFlag); 
        }

        #endregion
 
        #region IConvertable
        public TypeCode GetTypeCode() 
        { 
            Type enumType = this.GetType();
            Type underlyingType = GetUnderlyingType(enumType); 

            if (underlyingType == typeof(Int32))
            {
                return TypeCode.Int32; 
            }
 
            if (underlyingType == typeof(sbyte)) 
            {
                return TypeCode.SByte; 
            }

            if (underlyingType == typeof(Int16))
            { 
                return TypeCode.Int16;
            } 
 
            if (underlyingType == typeof(Int64))
            { 
                return TypeCode.Int64;
            }

            if (underlyingType == typeof(UInt32)) 
            {
                return TypeCode.UInt32; 
            } 

            if (underlyingType == typeof(byte)) 
            {
                return TypeCode.Byte;
            }
 
            if (underlyingType == typeof(UInt16))
            { 
                return TypeCode.UInt16; 
            }
 
            if (underlyingType == typeof(UInt64))
            {
                return TypeCode.UInt64;
            } 

            Contract.Assert(false, "Unknown underlying type."); 
            throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_UnknownEnumType")); 
        }
 
        /// <internalonly/>
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/> 
        char IConvertible.ToChar(IFormatProvider provider)
        { 
            return Convert.ToChar(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/> 
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        { 
            return Convert.ToSByte(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/>
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/> 
        short IConvertible.ToInt16(IFormatProvider provider)
        { 
            return Convert.ToInt16(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/> 
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        { 
            return Convert.ToUInt16(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/>
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/> 
        uint IConvertible.ToUInt32(IFormatProvider provider)
        { 
            return Convert.ToUInt32(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/> 
        long IConvertible.ToInt64(IFormatProvider provider)
        { 
            return Convert.ToInt64(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/>
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/> 
        float IConvertible.ToSingle(IFormatProvider provider)
        { 
            return Convert.ToSingle(GetValue(), CultureInfo.CurrentCulture);
        }

        /// <internalonly/> 
        double IConvertible.ToDouble(IFormatProvider provider)
        { 
            return Convert.ToDouble(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/>
        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(GetValue(), CultureInfo.CurrentCulture); 
        }
 
        /// <internalonly/> 
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        { 
            throw new InvalidCastException(Environment.GetResourceString("InvalidCast_FromTo", "Enum", "DateTime"));
        }

        /// <internalonly/> 
        Object IConvertible.ToType(Type type, IFormatProvider provider)
        { 
            return Convert.DefaultToType((IConvertible)this, type, provider); 
        }
        #endregion 

        #region ToObject
        [System.Security.SecuritySafeCritical]  // auto-generated
        [CLSCompliant(false)] 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, sbyte value) 
        { 
            if (enumType == null)
                throw new ArgumentNullException("enumType"); 
            if (!enumType.IsEnum)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
            Contract.EndContractBlock();
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType"); 
            return InternalBoxEnum(rtType, value); 
        }
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, short value)
        {
            if (enumType == null) 
                throw new ArgumentNullException("enumType");
            if (!enumType.IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType"); 
            Contract.EndContractBlock();
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType");
            return InternalBoxEnum(rtType, value);
        } 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(true)] 
        public static Object ToObject(Type enumType, int value) 
        {
            if (enumType == null) 
                throw new ArgumentNullException("enumType");
            if (!enumType.IsEnum)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
            Contract.EndContractBlock(); 
            RuntimeType rtType = enumType as RuntimeType;
            if (rtType == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType"); 
            return InternalBoxEnum(rtType, value);
        } 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, byte value)
        { 
            if (enumType == null)
                throw new ArgumentNullException("enumType"); 
            if (!enumType.IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
            Contract.EndContractBlock(); 
            RuntimeType rtType = enumType as RuntimeType;
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType");
            return InternalBoxEnum(rtType, value); 
        }
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [CLSCompliant(false)] 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, ushort value) 
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            if (!enumType.IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
            Contract.EndContractBlock(); 
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType"); 
            return InternalBoxEnum(rtType, value);
        }
        [System.Security.SecuritySafeCritical]  // auto-generated
        [CLSCompliant(false)] 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, uint value) 
        { 
            if (enumType == null)
                throw new ArgumentNullException("enumType"); 
            if (!enumType.IsEnum)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
            Contract.EndContractBlock();
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType"); 
            return InternalBoxEnum(rtType, value); 
        }
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [System.Runtime.InteropServices.ComVisible(true)]
        public static Object ToObject(Type enumType, long value)
        {
            if (enumType == null) 
                throw new ArgumentNullException("enumType");
            if (!enumType.IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType"); 
            Contract.EndContractBlock();
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType");
            return InternalBoxEnum(rtType, value);
        } 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [CLSCompliant(false)] 
        [System.Runtime.InteropServices.ComVisible(true)] 
        public static Object ToObject(Type enumType, ulong value)
        { 
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            if (!enumType.IsEnum)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType"); 
            Contract.EndContractBlock();
            RuntimeType rtType = enumType as RuntimeType; 
            if (rtType == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType");
            return InternalBoxEnum(rtType, unchecked((long)value)); 
        }
        #endregion
    }
} 

