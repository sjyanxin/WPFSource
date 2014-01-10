// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
#if FEATURE_CODEPAGES_FILE // requires BaseCodePageEncooding
namespace System.Text 
{ 
    using System;
    using System.Diagnostics.Contracts; 
    using System.Text;
    using System.Threading;
    using System.Runtime.Serialization;
    using System.Security.Permissions; 

    // DBCSCodePageEncoding 
    // 
    [Serializable]
    internal class DBCSCodePageEncoding : BaseCodePageEncoding, ISerializable 
    {
        // Pointers to our memory section parts
        [NonSerialized]
        protected unsafe char*   mapBytesToUnicode = null;      // char 65536 
        [NonSerialized]
        protected unsafe ushort* mapUnicodeToBytes = null;      // byte 65536 
        [NonSerialized] 
        protected unsafe int*    mapCodePageCached = null;      // to remember which CP is cached
 
        [NonSerialized]
        protected const char     UNKNOWN_CHAR_FLAG=(char)0x0;
        [NonSerialized]
        protected const char     UNICODE_REPLACEMENT_CHAR=(char)0xFFFD; 
        [NonSerialized]
        protected const char     LEAD_BYTE_CHAR=(char)0xFFFE;   // For lead bytes 
 
        // Note that even though we provide bytesUnknown and byteCountUnknown,
        // They aren't actually used because of the fallback mechanism. (char is though) 
        [NonSerialized]
        ushort  bytesUnknown;
        [NonSerialized]
        int     byteCountUnknown; 
        [NonSerialized]
        protected char    charUnknown = (char)0; 
 
        [System.Security.SecurityCritical]  // auto-generated
        public DBCSCodePageEncoding(int codePage) : this(codePage, codePage) 
        {
        }

        [System.Security.SecurityCritical]  // auto-generated 
        internal DBCSCodePageEncoding(int codePage, int dataCodePage) : base(codePage, dataCodePage)
        { 
        } 

        // Constructor called by serialization. 
        // Note:  We use the base GetObjectData however
        [System.Security.SecurityCritical]  // auto-generated
        internal DBCSCodePageEncoding(SerializationInfo info, StreamingContext context) : base(0)
        { 
            // Actually this can't ever get called, CodePageEncoding is our proxy
            Contract.Assert(false, "Didn't expect to make it to DBCSCodePageEncoding serialization constructor"); 
            throw new ArgumentNullException("this"); 
        }
 
        // MBCS data section:
        //
        // We treat each multibyte pattern as 2 bytes in our table.  If its a single byte, then the high byte
        // for that position will be 0.  When the table is loaded, leading bytes are flagged with 0xFFFE, so 
        // when reading the table look up with each byte.  If the result is 0xFFFE, then use 2 bytes to read
        // further data.  FFFF is a special value indicating that the unicode code is the same as the 
        // character code (this helps us support code points < 0x20).  FFFD is used as replacement character. 
        //
        // Normal table: 
        // WCHAR*     -  Starting with MB code point 0.
        //               FFFF indicates we are to use the multibyte value for our code point.
        //               FFFE is the lead byte mark.  (This should only appear in positions < 0x100)
        //               FFFD is the replacement (unknown character) mark. 
        //               2-20 means to advance the pointer 2-0x20 characters.
        //               1 means that to advance to the multibyte position contained in the next char. 
        //               0 nothing special (I don't think its possible.) 
        //
        // Table ends when multibyte position has advanced to 0xFFFF. 
        //
        // Bytes->Unicode Best Fit table:
        // WCHAR*     -  Same as normal table, except first wchar is byte position to start at.
        // 
        // Unicode->Bytes Best Fit Table:
        // WCHAR*     -  Same as normal table, except first wchar is char position to start at and 
        //               we loop through unicode code points and the table has the byte points that 
        //               corrospond to those unicode code points.
        // We have a managed code page entry, so load our tables 
        //
        [System.Security.SecurityCritical]  // auto-generated
        protected override unsafe void LoadManagedCodePage()
        { 
            // Should be loading OUR code page
            Contract.Assert(pCodePage->CodePage == this.dataTableCodePage, 
                "[DBCSCodePageEncoding.LoadManagedCodePage]Expected to load data table code page"); 

            // Make sure we're really a 1 byte code page 
            if (pCodePage->ByteCount != 2)
                throw new NotSupportedException(
                    Environment.GetResourceString("NotSupported_NoCodepageData", CodePage));
            // Remember our unknown bytes & chars 
            bytesUnknown = pCodePage->ByteReplace;
            charUnknown = pCodePage->UnicodeReplace; 
 
            // Need to make sure the fallback buffer's fallback char is correct
            if (this.DecoderFallback.IsMicrosoftBestFitFallback) 
            {
                ((InternalDecoderBestFitFallback)(this.DecoderFallback)).cReplacement = charUnknown;
            }
 
            // Is our replacement bytesUnknown a single or double byte character?
            byteCountUnknown = 1; 
            if (bytesUnknown > 0xff) 
                byteCountUnknown++;
 
            // We use fallback encoder, which uses ?, which so far all of our tables do as well
            Contract.Assert(bytesUnknown == 0x3f,
                "[DBCSCodePageEncoding.LoadManagedCodePage]Expected 0x3f (?) as unknown byte character");
 
            // Get our mapped section (bytes to allocate = 2 bytes per 65536 Unicode chars + 2 bytes per 65536 DBCS chars)
            // Plus 4 byte to remember CP # when done loading it. (Don't want to get IA64 or anything out of alignment) 
            byte *pMemorySection = GetSharedMemory(65536 * 2 * 2 + 4 + this.iExtraBytes); 

            mapBytesToUnicode = (char*)pMemorySection; 
            mapUnicodeToBytes = (ushort*)(pMemorySection + 65536 * 2);
            mapCodePageCached = (int*)(pMemorySection + 65536 * 2 * 2 + this.iExtraBytes);

            // If its cached (& filled in) we don't have to do anything else 
            if (*mapCodePageCached != 0)
            { 
                Contract.Assert(((*mapCodePageCached == this.dataTableCodePage && this.bFlagDataTable) || 
                    (*mapCodePageCached == this.CodePage && !this.bFlagDataTable)),
                    "[DBCSCodePageEncoding.LoadManagedCodePage]Expected mapped section cached page flag to be set to data table or regular code page."); 

                // Special case for GB18030 because it mangles its own code page after this function
                if ((*mapCodePageCached != this.dataTableCodePage && this.bFlagDataTable) ||
                    (*mapCodePageCached != this.CodePage && !this.bFlagDataTable)) 
                    throw new OutOfMemoryException(
                        Environment.GetResourceString("Arg_OutOfMemoryException")); 
 
                // If its cached (& filled in) we don't have to do anything else
                return; 
            }

            // Need to read our data file and fill in our section.
            // WARNING: Multiple code pieces could do this at once (so we don't have to lock machine-wide) 
            //          so be careful here.  Only stick legal values in here, don't stick temporary values.
 
            // Move to the beginning of the data section 
            char* pData = (char*)&(pCodePage->FirstDataWord);
 
            // We start at bytes position 0
            int bytePosition = 0;
            int useBytes = 0;
 
            while (bytePosition < 0x10000)
            { 
                // Get the next byte 
                char input = *pData;
                pData++; 

                // build our table:
                if (input == 1)
                { 
                    // Use next data as our byte position
                    bytePosition = (int)(*pData); 
                    pData++; 
                    continue;
                } 
                else if (input < 0x20 && input > 0)
                {
                    // Advance input characters
                    bytePosition += input; 
                    continue;
                } 
                else if (input == 0xFFFF) 
                {
                    // Same as our bytePosition 
                    useBytes = bytePosition;
                    input = unchecked((char)bytePosition);
                }
                else if (input == LEAD_BYTE_CHAR) // 0xfffe 
                {
                    // Lead byte mark 
                    Contract.Assert(bytePosition < 0x100, "[DBCSCodePageEncoding.LoadManagedCodePage]expected lead byte to be < 0x100"); 
                    useBytes = bytePosition;
                    // input stays 0xFFFE 
                }
                else if (input == UNICODE_REPLACEMENT_CHAR)
                {
                    // Replacement char is already done 
                    bytePosition++;
                    continue; 
                } 
                else
                { 
                    // Use this character
                    useBytes = bytePosition;
                    // input == input;
                } 

                // We may need to clean up the selected character & position 
                if (CleanUpBytes(ref useBytes)) 
                {
                    // Use this selected character at the selected position, don't do this if not supposed to. 
                   if (input != LEAD_BYTE_CHAR)
                    {
                        // Don't do this for lead byte marks.
                        mapUnicodeToBytes[input] = unchecked((ushort)useBytes); 
                    }
                    mapBytesToUnicode[useBytes] = input; 
                } 
                bytePosition++;
            } 

            // See if we have any clean up junk to do
            CleanUpEndBytes(mapBytesToUnicode);
 
            // We're done with our mapped section, set our flag so others don't have to rebuild table.
            // We only do this if we're flagging(using) the data table as our primary mechanism 
            if (this.bFlagDataTable) 
                *mapCodePageCached = this.dataTableCodePage;
        } 

        // Any special processing for this code page
        protected virtual bool CleanUpBytes(ref int bytes)
        { 
            return true;
        } 
 
        // Any special processing for this code page
        [System.Security.SecurityCritical]  // auto-generated 
        protected virtual unsafe void CleanUpEndBytes(char* chars)
        {
        }
 
        // Private object for locking instead of locking on a public type for SQL reliability work.
        private static Object s_InternalSyncObject; 
        private static Object InternalSyncObject 
        {
            get 
            {
                if (s_InternalSyncObject == null)
                {
                    Object o = new Object(); 
                    Interlocked.CompareExchange<Object>(ref s_InternalSyncObject, o, null);
                } 
                return s_InternalSyncObject; 
            }
        } 

        // Read in our best fit table
        [System.Security.SecurityCritical]  // auto-generated
        protected unsafe override void ReadBestFitTable() 
        {
            // Lock so we don't confuse ourselves. 
            lock(InternalSyncObject) 
            {
                // If we got a best fit array already then don't do this 
                if (arrayUnicodeBestFit == null)
                {
                    //
                    // Read in Best Fit table. 
                    //
 
                    // First we have to advance past original character mapping table 
                    // Move to the beginning of the data section
                    char* pData = (char*)&(pCodePage->FirstDataWord); 

                    // We start at bytes position 0
                    int bytesPosition = 0;
 
                    while (bytesPosition < 0x10000)
                    { 
                        // Get the next byte 
                        char input = *pData;
                        pData++; 

                        // build our table:
                        if (input == 1)
                        { 
                            // Use next data as our byte position
                            bytesPosition = (int)(*pData); 
                            pData++; 
                        }
                        else if (input < 0x20 && input > 0) 
                        {
                            // Advance input characters
                            bytesPosition += input;
                        } 
                        else
                        { 
                            // All other cases add 1 to bytes position 
                            bytesPosition++;
                        } 
                    }

                    // Now bytesPosition is at start of bytes->unicode best fit table
                    char* pBytes2Unicode = pData; 

                    // Now pData should be pointing to first word of bytes -> unicode best fit table 
                    // (which we're also not using at the moment) 
                    int iBestFitCount = 0;
                    bytesPosition = *pData; 
                    pData++;

                    while (bytesPosition < 0x10000)
                    { 
                        // Get the next byte
                        char input = *pData; 
                        pData++; 

                        // build our table: 
                        if (input == 1)
                        {
                            // Use next data as our byte position
                            bytesPosition = (int)(*pData); 
                            pData++;
                        } 
                        else if (input < 0x20 && input > 0) 
                        {
                            // Advance input characters 
                            bytesPosition += input;
                        }
                        else
                        { 
                            // Use this character (unless its unknown, unk just skips 1)
                            if (input != UNICODE_REPLACEMENT_CHAR) 
                            { 
                                int correctedChar = bytesPosition;
                                if (CleanUpBytes(ref correctedChar)) 
                                {
                                    // Sometimes correction makes them same as no best fit, skip those.
                                    if (mapBytesToUnicode[correctedChar] != input)
                                    { 
                                        iBestFitCount++;
                                    } 
                                } 
                            }
 
                            // Position gets incremented in any case.
                            bytesPosition++;
                        }
 
                    }
 
                    // Now we know how big the best fit table has to be 
                    char[] arrayTemp = new char[iBestFitCount * 2];
 
                    // Now we know how many best fits we have, so go back & read them in
                    iBestFitCount = 0;
                    pData = pBytes2Unicode;
                    bytesPosition = *pData; 
                    pData++;
                    bool bOutOfOrder = false; 
 
                    // Read it all in again
                    while (bytesPosition < 0x10000) 
                    {
                        // Get the next byte
                        char input = *pData;
                        pData++; 

                        // build our table: 
                        if (input == 1) 
                        {
                            // Use next data as our byte position 
                            bytesPosition = (int)(*pData);
                            pData++;
                        }
                        else if (input < 0x20 && input > 0) 
                        {
                            // Advance input characters 
                            bytesPosition += input; 
                        }
                        else 
                        {
                            // Use this character (unless its unknown, unk just skips 1)
                            if (input != UNICODE_REPLACEMENT_CHAR)
                            { 
                                int correctedChar = bytesPosition;
                                if (CleanUpBytes(ref correctedChar)) 
                                { 
                                    // Sometimes correction makes them same as no best fit, skip those.
                                    if (mapBytesToUnicode[correctedChar] != input) 
                                    {
                                        if (correctedChar != bytesPosition)
                                            bOutOfOrder = true;
 
                                        arrayTemp[iBestFitCount++] = unchecked((char)correctedChar);
                                        arrayTemp[iBestFitCount++] = input; 
                                    } 
                                }
                            } 

                            // Position gets incremented in any case.
                            bytesPosition++;
                        } 
                    }
 
                    // If they're out of order we need to sort them. 
                    if (bOutOfOrder)
                    { 
                        Contract.Assert((arrayTemp.Length / 2) < 20,
                            "[DBCSCodePageEncoding.ReadBestFitTable]Expected small best fit table < 20 for code page " + CodePage + ", not " + arrayTemp.Length / 2);

                        for (int i = 0; i < arrayTemp.Length - 2; i+=2) 
                        {
                            int iSmallest = i; 
                            char cSmallest = arrayTemp[i]; 

                            for (int j = i + 2; j < arrayTemp.Length; j+=2) 
                            {
                                // Find smallest one for front
                                if (cSmallest > arrayTemp[j])
                                { 
                                    cSmallest = arrayTemp[j];
                                    iSmallest = j; 
                                } 
                            }
 
                            // If smallest one is something else, switch them
                            if (iSmallest != i)
                            {
                                char temp = arrayTemp[iSmallest]; 
                                arrayTemp[iSmallest] = arrayTemp[i];
                                arrayTemp[i] = temp; 
                                temp = arrayTemp[iSmallest+1]; 
                                arrayTemp[iSmallest+1] = arrayTemp[i+1];
                                arrayTemp[i+1] = temp; 
                            }
                        }
                    }
 
                    // Remember our array
                    arrayBytesBestFit = arrayTemp; 
 
                    // Now were at beginning of Unicode -> Bytes best fit table, need to count them
                    char* pUnicode2Bytes = pData; 
                    int unicodePosition = *(pData++);
                    iBestFitCount = 0;

                    while (unicodePosition < 0x10000) 
                    {
                        // Get the next byte 
                        char input = *pData; 
                        pData++;
 
                        // build our table:
                        if (input == 1)
                        {
                            // Use next data as our byte position 
                            unicodePosition = (int)*pData;
                            pData++; 
                        } 
                        else if (input < 0x20 && input > 0)
                        { 
                            // Advance input characters
                            unicodePosition += input;
                        }
                        else 
                        {
                            // Same as our unicodePosition or use this character 
                            if (input > 0) 
                                iBestFitCount++;
                            unicodePosition++; 
                        }
                    }

                    // Allocate our table 
                    arrayTemp = new char[iBestFitCount*2];
 
                    // Now do it again to fill the array with real values 
                    pData = pUnicode2Bytes;
                    unicodePosition = *(pData++); 
                    iBestFitCount = 0;

                    while (unicodePosition < 0x10000)
                    { 
                        // Get the next byte
                        char input = *pData; 
                        pData++; 

                        // build our table: 
                        if (input == 1)
                        {
                            // Use next data as our byte position
                            unicodePosition = (int)*pData; 
                            pData++;
                        } 
                        else if (input < 0x20 && input > 0) 
                        {
                            // Advance input characters 
                            unicodePosition += input;
                        }
                        else
                        { 
                            if (input > 0)
                            { 
                                // Use this character, may need to clean it up 
                                int correctedChar = (int)input;
                                if (CleanUpBytes(ref correctedChar)) 
                                {
                                    arrayTemp[iBestFitCount++] = unchecked((char)unicodePosition);
                                    // Have to map it to Unicode because best fit will need unicode value of best fit char.
                                    arrayTemp[iBestFitCount++] = mapBytesToUnicode[correctedChar]; 

                                    // This won't work if it won't round trip. 
                                    // We can't do this assert for CP 51932 & 50220 because they aren't 
                                    // calling CleanUpBytes() for best fit.  All the string stuff here
                                    // also makes this assert slow. 
    //                                Contract.Assert(arrayTemp[iBestFitCount-1] != (char)0xFFFD, String.Format(
    //                                    "[DBCSCodePageEncoding.ReadBestFitTable] No valid Unicode value {0:X4} for round trip bytes {1:X4}, encoding {2}",
    //                                    (int)mapBytesToUnicode[input], (int)input, CodePage));
                                } 
                            }
                            unicodePosition++; 
                        } 
                    }
 
                    // Remember our array
                    arrayUnicodeBestFit = arrayTemp;
                }
 
            }
        } 
 
        // GetByteCount
        // Note: We start by assuming that the output will be the same as count.  Having 
        // an encoder or fallback may change that assumption
        [System.Security.SecurityCritical]  // auto-generated
        internal override unsafe int GetByteCount(char* chars, int count, EncoderNLS encoder)
        { 
            // Just need to ASSERT, this is called by something else internal that checked parameters already
            Contract.Assert(count >= 0, "[DBCSCodePageEncoding.GetByteCount]count is negative"); 
            Contract.Assert(chars != null, "[DBCSCodePageEncoding.GetByteCount]chars is null"); 

            // Assert because we shouldn't be able to have a null encoder. 
            Contract.Assert(encoderFallback != null, "[DBCSCodePageEncoding.GetByteCount]Attempting to use null fallback");

            CheckMemorySection();
 
            // Get any left over characters
            char charLeftOver = (char)0; 
            if (encoder != null) 
            {
                charLeftOver = encoder.charLeftOver; 

                // Only count if encoder.m_throwOnOverflow
                if (encoder.InternalHasFallbackBuffer && encoder.FallbackBuffer.Remaining > 0)
                    throw new ArgumentException(Environment.GetResourceString("Argument_EncoderFallbackNotEmpty", 
                        this.EncodingName, encoder.Fallback.GetType()));
            } 
 
            // prepare our end
            int byteCount = 0; 
            char* charEnd = chars + count;

            // For fallback we will need a fallback buffer
            EncoderFallbackBuffer fallbackBuffer = null; 

            // We may have a left over character from last time, try and process it. 
            if (charLeftOver > 0) 
            {
                Contract.Assert(Char.IsHighSurrogate(charLeftOver), "[DBCSCodePageEncoding.GetByteCount]leftover character should be high surrogate"); 
                Contract.Assert(encoder != null,
                    "[DBCSCodePageEncoding.GetByteCount]Expect to have encoder if we have a charLeftOver");

                // Since left over char was a surrogate, it'll have to be fallen back. 
                // Get Fallback
                fallbackBuffer = encoder.FallbackBuffer; 
                fallbackBuffer.InternalInitialize(chars, charEnd, encoder, false); 
                // This will fallback a pair if *chars is a low surrogate
                fallbackBuffer.InternalFallback(charLeftOver, ref chars); 
            }

            // Now we may have fallback char[] already (from the encoder)
 
            // We have to use fallback method.
            char ch; 
            while ((ch = (fallbackBuffer == null) ? '\0' : fallbackBuffer.InternalGetNextChar()) != 0 || 
                    chars < charEnd)
            { 
                // First unwind any fallback
                if (ch == 0)
                {
                    // No fallback, just get next char 
                    ch = *chars;
                    chars++; 
                } 

                // get byte for this char 
                ushort sTemp = mapUnicodeToBytes[ch];

                // Check for fallback, this'll catch surrogate pairs too.
                if (sTemp == 0 && ch != (char)0) 
                {
                    if (fallbackBuffer == null) 
                    { 
                        // Initialize the buffer
                        if (encoder == null) 
                            fallbackBuffer = this.encoderFallback.CreateFallbackBuffer();
                        else
                            fallbackBuffer = encoder.FallbackBuffer;
                        fallbackBuffer.InternalInitialize(charEnd - count, charEnd, encoder, false); 
                    }
 
                    // Get Fallback 
                    fallbackBuffer.InternalFallback(ch, ref chars);
                    continue; 
                }

                // We'll use this one
                byteCount++; 
                if (sTemp >= 0x100)
                    byteCount++; 
            } 

            return (int)byteCount; 
        }

        [System.Security.SecurityCritical]  // auto-generated
        internal override unsafe int GetBytes(char* chars, int charCount, 
                                                byte* bytes, int byteCount, EncoderNLS encoder)
        { 
            // Just need to ASSERT, this is called by something else internal that checked parameters already 
            Contract.Assert(bytes != null, "[DBCSCodePageEncoding.GetBytes]bytes is null");
            Contract.Assert(byteCount >= 0, "[DBCSCodePageEncoding.GetBytes]byteCount is negative"); 
            Contract.Assert(chars != null, "[DBCSCodePageEncoding.GetBytes]chars is null");
            Contract.Assert(charCount >= 0, "[DBCSCodePageEncoding.GetBytes]charCount is negative");

            // Assert because we shouldn't be able to have a null encoder. 
            Contract.Assert(encoderFallback != null, "[DBCSCodePageEncoding.GetBytes]Attempting to use null encoder fallback");
 
            CheckMemorySection(); 

            // For fallback we will need a fallback buffer 
            EncoderFallbackBuffer fallbackBuffer = null;

            // prepare our end
            char* charEnd = chars + charCount; 
            char* charStart = chars;
            byte* byteStart = bytes; 
            byte* byteEnd = bytes + byteCount; 

            // Get any left over characters 
            char charLeftOver = (char)0;
            if (encoder != null)
            {
                charLeftOver = encoder.charLeftOver; 
                Contract.Assert(charLeftOver == 0 || Char.IsHighSurrogate(charLeftOver),
                    "[DBCSCodePageEncoding.GetBytes]leftover character should be high surrogate"); 
 
                // Go ahead and get the fallback buffer (need leftover fallback if converting)
                fallbackBuffer = encoder.FallbackBuffer; 
                fallbackBuffer.InternalInitialize(chars, charEnd, encoder, true);

                // If we're not converting we must not have a fallback buffer
                if (encoder.m_throwOnOverflow && fallbackBuffer.Remaining > 0) 
                    throw new ArgumentException(Environment.GetResourceString("Argument_EncoderFallbackNotEmpty",
                        this.EncodingName, encoder.Fallback.GetType())); 
 
                // We may have a left over character from last time, try and process it.
                if (charLeftOver > 0) 
                {
                    Contract.Assert(encoder != null,
                        "[DBCSCodePageEncoding.GetBytes]Expect to have encoder if we have a charLeftOver");
 
                    // Since left over char was a surrogate, it'll have to be fallen back.
                    // Get Fallback 
                    fallbackBuffer.InternalFallback(charLeftOver, ref chars); 
                }
            } 

            // Now we may have fallback char[] already from the encoder

            // Go ahead and do it, including the fallback. 
            char ch;
            while ((ch = (fallbackBuffer == null) ? '\0' : fallbackBuffer.InternalGetNextChar()) != 0 || 
                    chars < charEnd) 
            {
                // First unwind any fallback 
                if (ch == 0)
                {
                    // No fallback, just get next char
                    ch = *chars; 
                    chars++;
                } 
 
                // get byte for this char
                ushort sTemp = mapUnicodeToBytes[ch]; 

                // Check for fallback, this'll catch surrogate pairs too.
                if (sTemp == 0 && ch != (char)0)
                { 
                    if (fallbackBuffer == null)
                    { 
                        // Initialize the buffer 
                        Contract.Assert(encoder == null,
                            "[DBCSCodePageEncoding.GetBytes]Expected delayed create fallback only if no encoder."); 
                        fallbackBuffer = this.encoderFallback.CreateFallbackBuffer();
                        fallbackBuffer.InternalInitialize(charEnd - charCount, charEnd, encoder, true);
                    }
 
                    // Get Fallback
                    fallbackBuffer.InternalFallback(ch, ref chars); 
                    continue; 
                }
 
                // We'll use this one (or two)
                // Bounds check

                // Go ahead and add it, lead byte 1st if necessary 
                if (sTemp >= 0x100)
                { 
                    if (bytes + 1 >= byteEnd) 
                    {
                        // didn't use this char, we'll throw or use buffer 
                        if (fallbackBuffer == null || fallbackBuffer.bFallingBack == false)
                        {
                            Contract.Assert(chars > charStart,
                                "[DBCSCodePageEncoding.GetBytes]Expected chars to have advanced (double byte case)"); 
                            chars--;                                        // don't use last char
                        } 
                        else 
                            fallbackBuffer.MovePrevious();                  // don't use last fallback
                        ThrowBytesOverflow(encoder, chars == charStart);    // throw ? 
                        break;                                              // don't throw, stop
                    }

                    *bytes = unchecked((byte)(sTemp >> 8)); 
                    bytes++;
                } 
                // Single byte 
                else if (bytes >= byteEnd)
                { 
                    // didn't use this char, we'll throw or use buffer
                    if (fallbackBuffer == null || fallbackBuffer.bFallingBack == false)
                    {
                        Contract.Assert(chars > charStart, 
                            "[DBCSCodePageEncoding.GetBytes]Expected chars to have advanced (single byte case)");
                        chars--;                                        // don't use last char 
                    } 
                    else
                        fallbackBuffer.MovePrevious();                  // don't use last fallback 
                    ThrowBytesOverflow(encoder, chars == charStart);    // throw ?
                    break;                                              // don't throw, stop
                }
 
                *bytes = unchecked((byte)(sTemp & 0xff));
                bytes++; 
            } 

            // encoder stuff if we have one 
            if (encoder != null)
            {
                // Fallback stuck it in encoder if necessary, but we have to clear MustFlush cases
                if (fallbackBuffer != null && !fallbackBuffer.bUsedEncoder) 
                    // Clear it in case of MustFlush
                    encoder.charLeftOver = (char)0; 
 
                // Set our chars used count
                encoder.m_charsUsed = (int)(chars - charStart); 
            }

            // If we're not converting we must not have a fallback buffer
            // (We don't really have a way to clear none-encoder using fallbacks however) 
//            Contract.Assert((encoder == null || encoder.m_throwOnOverflow) &&
//                (fallbackBuffer == null || fallbackBuffer.Remaining == 0), 
//                "[DBCSEncoding.GetBytes]Expected empty fallback buffer at end if not converting"); 

            return (int)(bytes - byteStart); 
        }

        // This is internal and called by something else,
        [System.Security.SecurityCritical]  // auto-generated 
        internal override unsafe int GetCharCount(byte* bytes, int count, DecoderNLS baseDecoder)
        { 
            // Just assert, we're called internally so these should be safe, checked already 
            Contract.Assert(bytes != null, "[DBCSCodePageEncoding.GetCharCount]bytes is null");
            Contract.Assert(count >= 0, "[DBCSCodePageEncoding.GetCharCount]byteCount is negative"); 

            CheckMemorySection();

            // Fix our decoder 
            DBCSDecoder decoder = (DBCSDecoder)baseDecoder;
 
            // Get our fallback 
            DecoderFallbackBuffer fallbackBuffer = null;
 
            // We'll need to know where the end is
            byte* byteEnd = bytes + count;
            int charCount = count;  // Assume 1 char / byte
 
            // Shouldn't have anything in fallback buffer for GetCharCount
            // (don't have to check m_throwOnOverflow for count) 
            Contract.Assert(decoder == null || 
                !decoder.InternalHasFallbackBuffer || decoder.FallbackBuffer.Remaining == 0,
                "[DBCSCodePageEncoding.GetCharCount]Expected empty fallback buffer at start"); 

            // If we have a left over byte, use it
            if (decoder != null && decoder.bLeftOver > 0)
            { 
                // We have a left over byte?
                if (count == 0) 
                { 
                    // No input though
                    if (!decoder.MustFlush) 
                    {
                        // Don't have to flush
                        return 0;
                    } 

 
                    Contract.Assert(fallbackBuffer == null, 
                        "[DBCSCodePageEncoding.GetCharCount]Expected empty fallback buffer");
                    fallbackBuffer = decoder.FallbackBuffer; 
                    fallbackBuffer.InternalInitialize(bytes, null);

                    byte[] byteBuffer = new byte[] { unchecked((byte)decoder.bLeftOver) };
                    return fallbackBuffer.InternalFallback(byteBuffer, bytes); 
                }
 
                // Get our full info 
                int iBytes = decoder.bLeftOver << 8;
                iBytes |= (*bytes); 
                bytes++;

                // This is either 1 known char or fallback
                // Already counted 1 char 
                // Look up our bytes
                char cDecoder = mapBytesToUnicode[iBytes]; 
                if (cDecoder == 0 && iBytes != 0) 
                {
                    // Deallocate preallocated one 
                    charCount--;

                    // We'll need a fallback
                    Contract.Assert(fallbackBuffer == null, 
                        "[DBCSCodePageEncoding.GetCharCount]Expected empty fallback buffer for unknown pair");
                    fallbackBuffer = decoder.FallbackBuffer; 
                    fallbackBuffer.InternalInitialize(byteEnd - count, null); 

                    // Do fallback, we know there're 2 bytes 
                    byte[] byteBuffer = new byte[] { unchecked((byte)(iBytes >> 8)), unchecked((byte)iBytes) };
                    charCount += fallbackBuffer.InternalFallback(byteBuffer, bytes);
                }
                // else we already reserved space for this one. 
            }
 
            // Loop, watch out for fallbacks 
            while (bytes < byteEnd)
            { 
                // Faster if don't use *bytes++;
                int iBytes = *bytes;
                bytes++;
                char c = mapBytesToUnicode[iBytes]; 

                // See if it was a double byte character 
                if (c == LEAD_BYTE_CHAR) 
                {
                    // Its a lead byte 
                    charCount--; // deallocate preallocated lead byte
                    if (bytes < byteEnd)
                    {
                        // Have another to use, so use it 
                        iBytes <<= 8;
                        iBytes |= *bytes; 
                        bytes++; 
                        c = mapBytesToUnicode[iBytes];
                    } 
                    else
                    {
                        // No input left
                        if (decoder == null || decoder.MustFlush) 
                        {
                            // have to flush anyway, set to unknown so we use fallback in a 'sec 
                            charCount++; // reallocate deallocated lead byte 
                            c = UNKNOWN_CHAR_FLAG;
                        } 
                        else
                        {
                            // We'll stick it in decoder
                            break; 
                        }
                    } 
                } 

                // See if it was unknown. 
                // Unknown and known chars already allocated, but fallbacks aren't
                if (c == UNKNOWN_CHAR_FLAG && iBytes != 0)
                {
                    if (fallbackBuffer == null) 
                    {
                        if (decoder == null) 
                            fallbackBuffer = this.DecoderFallback.CreateFallbackBuffer(); 
                        else
                            fallbackBuffer = decoder.FallbackBuffer; 
                        fallbackBuffer.InternalInitialize(byteEnd - count, null);
                    }

                    // Do fallback 
                    charCount--;    // Get rid of preallocated extra char
                    byte[] byteBuffer = null; 
                    if (iBytes < 0x100) 
                        byteBuffer = new byte[] { unchecked((byte)iBytes) };
                    else 
                        byteBuffer = new byte[] { unchecked((byte)(iBytes >> 8)), unchecked((byte)iBytes) };
                    charCount += fallbackBuffer.InternalFallback(byteBuffer, bytes);
                }
            } 

            // Shouldn't have anything in fallback buffer for GetChars 
            Contract.Assert(decoder == null || !decoder.m_throwOnOverflow || 
                !decoder.InternalHasFallbackBuffer || decoder.FallbackBuffer.Remaining == 0,
                "[DBCSCodePageEncoding.GetCharCount]Expected empty fallback buffer at end"); 

            // Return our count
            return charCount;
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal override unsafe int GetChars(byte* bytes, int byteCount, 
                                                char* chars, int charCount, DecoderNLS baseDecoder)
        { 
            // Just need to ASSERT, this is called by something else internal that checked parameters already
            Contract.Assert(bytes != null, "[DBCSCodePageEncoding.GetChars]bytes is null");
            Contract.Assert(byteCount >= 0, "[DBCSCodePageEncoding.GetChars]byteCount is negative");
            Contract.Assert(chars != null, "[DBCSCodePageEncoding.GetChars]chars is null"); 
            Contract.Assert(charCount >= 0, "[DBCSCodePageEncoding.GetChars]charCount is negative");
 
            CheckMemorySection(); 

            // Fix our decoder 
            DBCSDecoder decoder = (DBCSDecoder)baseDecoder;

            // We'll need to know where the end is
            byte* byteStart = bytes; 
            byte* byteEnd = bytes + byteCount;
            char* charStart = chars; 
            char* charEnd = chars + charCount; 
            bool bUsedDecoder = false;
 
            // Get our fallback
            DecoderFallbackBuffer fallbackBuffer = null;

            // Shouldn't have anything in fallback buffer for GetChars 
            Contract.Assert(decoder == null || !decoder.m_throwOnOverflow ||
                !decoder.InternalHasFallbackBuffer || decoder.FallbackBuffer.Remaining == 0, 
                "[DBCSCodePageEncoding.GetChars]Expected empty fallback buffer at start"); 

            // If we have a left over byte, use it 
            if (decoder != null && decoder.bLeftOver > 0)
            {
                // We have a left over byte?
                if (byteCount == 0) 
                {
                    // No input though 
                    if (!decoder.MustFlush) 
                    {
                        // Don't have to flush 
                        return 0;
                    }

                    // Well, we're flushing, so use '?' or fallback 
                    // fallback leftover byte
                    Contract.Assert(fallbackBuffer == null, 
                        "[DBCSCodePageEncoding.GetChars]Expected empty fallback"); 
                    fallbackBuffer = decoder.FallbackBuffer;
                    fallbackBuffer.InternalInitialize(bytes, charEnd); 

                    // If no room its hopeless, this was 1st fallback
                    byte[] byteBuffer = new byte[] { unchecked((byte)decoder.bLeftOver) };
                    if (!fallbackBuffer.InternalFallback(byteBuffer, bytes, ref chars)) 
                        ThrowCharsOverflow(decoder, true);
 
                    decoder.bLeftOver = 0; 

                    // Done, return it 
                    return (int)(chars-charStart);
                }

                // Get our full info 
                int iBytes = decoder.bLeftOver << 8;
                iBytes |= (*bytes); 
                bytes++; 

                // Look up our bytes 
                char cDecoder = mapBytesToUnicode[iBytes];
                if (cDecoder == UNKNOWN_CHAR_FLAG && iBytes != 0)
                {
                    Contract.Assert(fallbackBuffer == null, 
                        "[DBCSCodePageEncoding.GetChars]Expected empty fallback for two bytes");
                    fallbackBuffer = decoder.FallbackBuffer; 
                    fallbackBuffer.InternalInitialize(byteEnd - byteCount, charEnd); 

                    byte[] byteBuffer = new byte[] { unchecked((byte)(iBytes >> 8)), unchecked((byte)iBytes) }; 
                    if (!fallbackBuffer.InternalFallback(byteBuffer, bytes, ref chars))
                        ThrowCharsOverflow(decoder, true);
                }
                else 
                {
                    // Do we have output room?, hopeless if not, this is first char 
                    if (chars >= charEnd) 
                        ThrowCharsOverflow(decoder, true);
 
                    *(chars++) = cDecoder;
                }
            }
 
            // Loop, paying attention to our fallbacks.
            while (bytes < byteEnd) 
            { 
                // Faster if don't use *bytes++;
                int iBytes = *bytes; 
                bytes++;
                char c = mapBytesToUnicode[iBytes];

                // See if it was a double byte character 
                if (c == LEAD_BYTE_CHAR)
                { 
                    // Its a lead byte 
                    if (bytes < byteEnd)
                    { 
                        // Have another to use, so use it
                        iBytes <<= 8;
                        iBytes |= *bytes;
                        bytes++; 
                        c = mapBytesToUnicode[iBytes];
                    } 
                    else 
                    {
                        // No input left 
                        if (decoder == null || decoder.MustFlush)
                        {
                            // have to flush anyway, set to unknown so we use fallback in a 'sec
                            c = UNKNOWN_CHAR_FLAG; 
                        }
                        else 
                        { 
                            // Stick it in decoder
                            bUsedDecoder = true; 
                            decoder.bLeftOver = (byte)iBytes;
                            break;
                        }
                    } 
                }
 
                // See if it was unknown 
                if (c == UNKNOWN_CHAR_FLAG && iBytes != 0)
                { 
                    if (fallbackBuffer == null)
                    {
                        if (decoder == null)
                            fallbackBuffer = this.DecoderFallback.CreateFallbackBuffer(); 
                        else
                            fallbackBuffer = decoder.FallbackBuffer; 
                        fallbackBuffer.InternalInitialize(byteEnd - byteCount, charEnd); 
                    }
 
                    // Do fallback
                    byte[] byteBuffer = null;
                    if (iBytes < 0x100)
                        byteBuffer = new byte[] { unchecked((byte)iBytes) }; 
                    else
                        byteBuffer = new byte[] { unchecked((byte)(iBytes >> 8)), unchecked((byte)iBytes) }; 
                    if (!fallbackBuffer.InternalFallback(byteBuffer, bytes, ref chars)) 
                    {
                        // May or may not throw, but we didn't get these byte(s) 
                        Contract.Assert(bytes >= byteStart + byteBuffer.Length,
                            "[DBCSCodePageEncoding.GetChars]Expected bytes to have advanced for fallback");
                        bytes-=byteBuffer.Length;                           // didn't use these byte(s)
                        fallbackBuffer.InternalReset();                     // Didn't fall this back 
                        ThrowCharsOverflow(decoder, bytes == byteStart);    // throw?
                        break;                                              // don't throw, but stop loop 
                    } 
                }
                else 
                {
                    // Do we have buffer room?
                    if (chars >= charEnd)
                    { 
                        // May or may not throw, but we didn't get these byte(s)
                        Contract.Assert(bytes > byteStart, 
                            "[DBCSCodePageEncoding.GetChars]Expected bytes to have advanced for lead byte"); 
                        bytes--;                                            // unused byte
                        if (iBytes >= 0x100) 
                        {
                            Contract.Assert(bytes > byteStart,
                                "[DBCSCodePageEncoding.GetChars]Expected bytes to have advanced for trail byte");
                            bytes--;                                        // 2nd unused byte 
                        }
                        ThrowCharsOverflow(decoder, bytes == byteStart);    // throw? 
                        break;                                              // don't throw, but stop loop 
                    }
 
                    *(chars++) = c;
                }
            }
 
            // We already stuck it in encoder if necessary, but we have to clear cases where nothing new got into decoder
            if (decoder != null) 
            { 
                // Clear it in case of MustFlush
                if (bUsedDecoder == false) 
                {
                    decoder.bLeftOver = 0;
                }
 
                // Remember our count
                decoder.m_bytesUsed = (int)(bytes - byteStart); 
            } 

            // Shouldn't have anything in fallback buffer for GetChars 
            Contract.Assert(decoder == null || !decoder.m_throwOnOverflow ||
                !decoder.InternalHasFallbackBuffer || decoder.FallbackBuffer.Remaining == 0,
                "[DBCSCodePageEncoding.GetChars]Expected empty fallback buffer at end");
 
            // Return length of our output
            return (int)(chars - charStart); 
        } 

        public override int GetMaxByteCount(int charCount) 
        {
            if (charCount < 0)
               throw new ArgumentOutOfRangeException("charCount",
                    Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum")); 
            Contract.EndContractBlock();
 
            // Characters would be # of characters + 1 in case high surrogate is ? * max fallback 
            long byteCount = (long)charCount + 1;
 
            if (EncoderFallback.MaxCharCount > 1)
                byteCount *= EncoderFallback.MaxCharCount;

            // 2 to 1 is worst case.  Already considered surrogate fallback 
            byteCount *= 2;
 
            if (byteCount > 0x7fffffff) 
                throw new ArgumentOutOfRangeException("charCount", Environment.GetResourceString("ArgumentOutOfRange_GetByteCountOverflow"));
 
            return (int)byteCount;
        }

        public override int GetMaxCharCount(int byteCount) 
        {
            if (byteCount < 0) 
               throw new ArgumentOutOfRangeException("byteCount", 
                    Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            Contract.EndContractBlock(); 

            // DBCS is pretty much the same, but could have hanging high byte making extra ? and fallback for unknown
            long charCount = ((long)byteCount + 1);
 
            // 1 to 1 for most characters.  Only surrogates with fallbacks have less, unknown fallbacks could be longer.
            if (DecoderFallback.MaxCharCount > 1) 
                charCount *= DecoderFallback.MaxCharCount; 

            if (charCount > 0x7fffffff) 
                throw new ArgumentOutOfRangeException("byteCount", Environment.GetResourceString("ArgumentOutOfRange_GetCharCountOverflow"));

            return (int)charCount;
        } 

        public override Decoder GetDecoder() 
        { 
            return new DBCSDecoder(this);
        } 

        [Serializable]
        internal class DBCSDecoder : DecoderNLS
        { 
            // Need a place for the last left over byte
            internal byte bLeftOver = 0; 
 
            public DBCSDecoder(DBCSCodePageEncoding encoding) : base(encoding)
            { 
                // Base calls reset
            }

            public override void Reset() 
            {
                this.bLeftOver = 0; 
                if (m_fallbackBuffer != null) 
                    m_fallbackBuffer.Reset();
            } 

            // Anything left in our decoder?
            internal override bool HasState
            { 
                get
                { 
                    return (this.bLeftOver != 0); 
                }
            } 
        }
    }
}
#endif // FEATURE_CODEPAGES_FILE 


