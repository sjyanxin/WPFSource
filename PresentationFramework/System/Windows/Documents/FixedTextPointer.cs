//---------------------------------------------------------------------------- 
//
// File: FixedTextPointer.cs
//
// Copyright (C) 2004 by Microsoft Corporation.  All rights reserved. 
//
// Description: 
//      FixedTextPointer is an implementation of TextPointer/TextNavigator 
//      for Fixed Document. It is the base class for FixedTextPosition and
//      FixedTextPointer. 
//
//---------------------------------------------------------------------------

#pragma warning disable 1634, 1691 // To enable presharp warning disables (#pragma suppress) below. 

namespace System.Windows.Documents 
{ 
    using MS.Utility;
    using System.Windows; 
    using System;
    using System.Diagnostics;
    using MS.Internal;
 

    /// <summary> 
    ///  FixedTextPointer is an implementation of TextPointer/TextNavigator 
    ///  for Fixed Document.
    /// </summary> 
    /// <remarks>
    /// A FixedTextPointer is represented by a FlowPosition in the backing store
    /// </remarks>
    internal class FixedTextPointer : ContentPosition, ITextPointer 
    {
        //----------------------------------------------------- 
        // 
        //  Constructors
        // 
        //-----------------------------------------------------
        #region Constructors
        internal FixedTextPointer(bool mutable, LogicalDirection gravity, FlowPosition flow)
        { 
            _isFrozen = !mutable;
            _gravity = gravity; 
            _flowPosition = flow; 
        }
        #endregion Constructors 

        //------------------------------------------------------
        //
        //  Public Methods 
        //
        //----------------------------------------------------- 
 
#if DEBUG
        /// <summary> 
        /// Debug only ToString override.
        /// </summary>
        public override string ToString()
        { 
            return  "FTP"
                    + DebugId + " " 
                    +  (this._isFrozen? "NV " : "PO ") 
                    +  _flowPosition.ToString()
                    +  " " + this._gravity; 
        }
#endif // DEBUG

        /// <summary> 
        /// <see cref="TextPointer.CompareTo"/>
        /// </summary> 
        internal int CompareTo(ITextPointer position) 
        {
            FixedTextPointer ftp = this.FixedTextContainer.VerifyPosition(position); 

            return _flowPosition.CompareTo(ftp.FlowPosition);
        }
 
        int ITextPointer.CompareTo(StaticTextPointer position)
        { 
            return ((ITextPointer)this).CompareTo((ITextPointer)position.Handle0); 
        }
 
        #region TextPointer Methods

        /// <summary>
        /// <see cref="TextPointer.CompareTo"/> 
        /// </summary>
        int ITextPointer.CompareTo(ITextPointer position) 
        { 
            return CompareTo(position);
        } 

        /// <summary>
        /// <see cref="TextPointer.GetOffsetToPosition"/>
        /// </summary> 
        int ITextPointer.GetOffsetToPosition(ITextPointer position)
        { 
            FixedTextPointer ftp = this.FixedTextContainer.VerifyPosition(position); 

            return _flowPosition.GetDistance(ftp.FlowPosition); 
        }

        /// <summary>
        /// <see cref="TextPointer.GetPointerContext"/> 
        /// </summary>
        TextPointerContext ITextPointer.GetPointerContext(LogicalDirection direction) 
        { 
            ValidationHelper.VerifyDirection(direction, "direction");
            return _flowPosition.GetPointerContext(direction); 
        }

        /// <summary>
        /// <see cref="TextPointer.GetTextRunLength"/> 
        /// </summary>
        /// <remarks>Return 0 if non-text run</remarks> 
        int ITextPointer.GetTextRunLength(LogicalDirection direction) 
        {
            ValidationHelper.VerifyDirection(direction, "direction"); 
            if (_flowPosition.GetPointerContext(direction) != TextPointerContext.Text)
            {
                return 0;
            } 
            return _flowPosition.GetTextRunLength(direction);
        } 
 
        // <see cref="System.Windows.Documents.ITextPointer.GetTextInRun"/>
        string ITextPointer.GetTextInRun(LogicalDirection direction) 
        {
            return TextPointerBase.GetTextInRun(this, direction);
        }
 
        /// <summary>
        /// <see cref="TextPointer.GetTextInRun"/> 
        /// </summary> 
        /// <remarks>Only reutrn uninterrupted runs of text</remarks>
        int ITextPointer.GetTextInRun(LogicalDirection direction, char[] textBuffer, int startIndex, int count) 
        {
            ValidationHelper.VerifyDirection(direction, "direction");
            if (textBuffer == null)
            { 
                throw new ArgumentNullException("textBuffer");
            } 
            if (count < 0) 
            {
                throw new ArgumentException(SR.Get(SRID.NegativeValue, "count")); 
            }

            if (_flowPosition.GetPointerContext(direction) != TextPointerContext.Text)
            { 
                return 0;
            } 
            return _flowPosition.GetTextInRun(direction, count, textBuffer, startIndex); 
        }
 
        /// <summary>
        /// <see cref="TextPointer.GetAdjacentElement"/>
        /// </summary>
        /// <remarks>Return null if the embedded object does not exist</remarks> 
        object ITextPointer.GetAdjacentElement(LogicalDirection direction)
        { 
            ValidationHelper.VerifyDirection(direction, "direction"); 
            TextPointerContext tpc = _flowPosition.GetPointerContext(direction);
            if (!(tpc == TextPointerContext.EmbeddedElement || tpc == TextPointerContext.ElementStart || tpc == TextPointerContext.ElementEnd)) 
            {
                return null;
            }
            return _flowPosition.GetAdjacentElement(direction); 
        }
 
        /// <summary> 
        /// <see cref="TextPointer.GetElementType"/>
        /// </summary> 
        /// <remarks>Return null if no TextElement in the direction</remarks>
        Type ITextPointer.GetElementType(LogicalDirection direction)
        {
            ValidationHelper.VerifyDirection(direction, "direction"); 

            TextPointerContext tt = _flowPosition.GetPointerContext(direction); 
 
            if (tt == TextPointerContext.ElementStart || tt == TextPointerContext.ElementEnd)
            { 
                FixedElement e = _flowPosition.GetElement(direction);
                return e.IsTextElement ? e.Type : null;
            }
 
            return null;
        } 
 
        /// <summary>
        /// <see cref="TextPointer.HasEqualScope"/> 
        /// </summary>
        bool ITextPointer.HasEqualScope(ITextPointer position)
        {
            FixedTextPointer ftp = this.FixedTextContainer.VerifyPosition(position); 

            FixedElement thisFE = _flowPosition.GetScopingElement(); 
            FixedElement thatFE = ftp.FlowPosition.GetScopingElement(); 
            // We retun true even if both scoping elements are the
            // container element. 
            return thisFE == thatFE;
        }

        /// <summary> 
        /// <see cref="TextPointer.GetValue"/>
        /// </summary> 
        /// <remarks>return property values even if there is no scoping element</remarks> 
        object ITextPointer.GetValue(DependencyProperty property)
        { 
            if (property == null)
            {
                throw new ArgumentNullException("property");
            } 

            FixedElement e = _flowPosition.GetScopingElement(); 
            return e.GetValue(property); 
        }
 
        /// <summary>
        /// <see cref="TextPointer.ReadLocalValue"/>
        /// </summary>
        /// <remarks>Throws InvalidOperationException if there is no scoping element</remarks> 
        object ITextPointer.ReadLocalValue(DependencyProperty property)
        { 
            if (property == null) 
            {
                throw new ArgumentNullException("property"); 
            }

            FixedElement e = _flowPosition.GetScopingElement();
            if (!e.IsTextElement) 
            {
                throw new InvalidOperationException(SR.Get(SRID.NoElementObject)); 
            } 

            return e.ReadLocalValue(property); 
        }

        /// <summary>
        /// <see cref="TextPointer.GetLocalValueEnumerator"/> 
        /// </summary>
        /// <remarks>Returns an empty enumerator if there is no scoping element</remarks> 
        LocalValueEnumerator ITextPointer.GetLocalValueEnumerator() 
        {
            FixedElement e = _flowPosition.GetScopingElement(); 

            if (!e.IsTextElement)
            {
                return (new DependencyObject()).GetLocalValueEnumerator(); 
            }
 
            return e.GetLocalValueEnumerator(); 
        }
 
        ITextPointer ITextPointer.CreatePointer()
        {
            return ((ITextPointer)this).CreatePointer(0, ((ITextPointer)this).LogicalDirection);
        } 

        // Unoptimized CreateStaticPointer implementation. 
        // Creates a simple wrapper for an ITextPointer instance. 
        StaticTextPointer ITextPointer.CreateStaticPointer()
        { 
            return new StaticTextPointer(((ITextPointer)this).TextContainer, ((ITextPointer)this).CreatePointer());
        }

        ITextPointer ITextPointer.CreatePointer(int distance) 
        {
            return ((ITextPointer)this).CreatePointer(distance, ((ITextPointer)this).LogicalDirection); 
        } 

        ITextPointer ITextPointer.CreatePointer(LogicalDirection gravity) 
        {
            return ((ITextPointer)this).CreatePointer(0, gravity);
        }
 
        /// <summary>
        /// <see cref="TextPointer.CreatePointer"/> 
        /// </summary> 
        ITextPointer ITextPointer.CreatePointer(int distance, LogicalDirection gravity)
        { 
            ValidationHelper.VerifyDirection(gravity, "gravity");

            FlowPosition fp = (FlowPosition)_flowPosition.Clone();
            if (!fp.Move(distance)) 
            {
                throw new ArgumentException(SR.Get(SRID.BadDistance), "distance"); 
            } 

            return new FixedTextPointer(true, gravity, fp); 
        }

        // <see cref="ITextPointer.Freeze"/>
        void ITextPointer.Freeze() 
        {
            _isFrozen = true; 
        } 

        /// <summary> 
        /// <see cref="TextPointer.GetFrozenPosition"/>
        /// </summary>
        ITextPointer ITextPointer.GetFrozenPointer(LogicalDirection logicalDirection)
        { 
            return TextPointerBase.GetFrozenPointer(this, logicalDirection);
        } 
 
        // Candidate for replacing MoveToNextContextPosition for immutable TextPointer model
        ITextPointer ITextPointer.GetNextContextPosition(LogicalDirection direction) 
        {
            ITextPointer pointer = ((ITextPointer)this).CreatePointer();
            if (pointer.MoveToNextContextPosition(direction))
            { 
                pointer.Freeze();
            } 
            else 
            {
                pointer = null; 
            }
            return pointer;
        }
 
        // Candidate for replacing MoveToInsertionPosition for immutable TextPointer model
        ITextPointer ITextPointer.GetInsertionPosition(LogicalDirection direction) 
        { 
            ITextPointer pointer = ((ITextPointer)this).CreatePointer();
            pointer.MoveToInsertionPosition(direction); 
            pointer.Freeze();
            return pointer;
        }
 
        // Returns the closest insertion position, treating all unicode code points
        // as valid insertion positions.  A useful performance win over 
        // GetNextInsertionPosition when only formatting scopes are important. 
        ITextPointer ITextPointer.GetFormatNormalizedPosition(LogicalDirection direction)
        { 
            ITextPointer pointer = ((ITextPointer)this).CreatePointer();
            TextPointerBase.MoveToFormatNormalizedPosition(pointer, direction);
            pointer.Freeze();
            return pointer; 
        }
 
        // Candidate for replacing MoveToNextInsertionPosition for immutable TextPointer model 
        ITextPointer ITextPointer.GetNextInsertionPosition(LogicalDirection direction)
        { 
            ITextPointer pointer = ((ITextPointer)this).CreatePointer();
            if (pointer.MoveToNextInsertionPosition(direction))
            {
                pointer.Freeze(); 
            }
            else 
            { 
                pointer = null;
            } 
            return pointer;
        }

        #endregion TextPointer Methods 

        /// <summary> 
        /// <see cref="ITextPointer.SetLogicalDirection"/> 
        /// </summary>
        /// <param name="direction"></param> 
        void ITextPointer.SetLogicalDirection(LogicalDirection direction)
        {
            this.LogicalDirection = direction;
        } 

        #region TextNavigator Methods 
 
        /// <summary>
        /// <see cref="TextPointer.MoveByOffset"/> 
        /// </summary>
        bool ITextPointer.MoveToNextContextPosition(LogicalDirection direction)
        {
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!"); 

            ValidationHelper.VerifyDirection(direction, "direction"); 
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!"); 

            return _flowPosition.Move(direction); 
        }


        /// <summary> 
        /// <see cref="TextPointer.MoveByOffset"/>
        /// </summary> 
        int ITextPointer.MoveByOffset(int offset) 
        {
            if (_isFrozen) throw new InvalidOperationException(SR.Get(SRID.TextPositionIsFrozen)); 

            if (!_flowPosition.Move(offset))
            {
                throw new ArgumentException(SR.Get(SRID.BadDistance), "offset"); 
            }
            else 
            { 
                return offset;
            } 
        }

        /// <summary>
        /// <see cref="TextPointer.MoveToPosition"/> 
        /// </summary>
        void ITextPointer.MoveToPosition(ITextPointer position) 
        { 
            FixedTextPointer ftp = this.FixedTextContainer.VerifyPosition(position);
 
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!");

            _flowPosition.MoveTo(ftp.FlowPosition);
        } 

        /// <summary> 
        /// <see cref="TextPointer.MoveToElementEdge"/> 
        /// </summary>
        void ITextPointer.MoveToElementEdge(ElementEdge edge) 
        {
            ValidationHelper.VerifyElementEdge(edge, "edge");
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!");
 
            FixedElement e = _flowPosition.GetScopingElement();
            if (!e.IsTextElement) 
            { 
                throw new InvalidOperationException(SR.Get(SRID.NoElementObject));
            } 

            switch (edge)
            {
                case ElementEdge.BeforeStart: 
                    _flowPosition = (FlowPosition)e.Start.FlowPosition.Clone();
                    _flowPosition.Move(-1); 
                    break; 

                case ElementEdge.AfterStart: 
                    _flowPosition = (FlowPosition)e.Start.FlowPosition.Clone();
                    break;

                case ElementEdge.BeforeEnd: 
                    _flowPosition = (FlowPosition)e.End.FlowPosition.Clone();
                    break; 
 
                case ElementEdge.AfterEnd:
                    _flowPosition = (FlowPosition)e.End.FlowPosition.Clone(); 
                    _flowPosition.Move(1);
                    break;
            }
        } 

        // <see cref="TextPointer.MoveToLineBoundary"/> 
        int ITextPointer.MoveToLineBoundary(int count) 
        {
            return TextPointerBase.MoveToLineBoundary(this, ((ITextPointer)this).TextContainer.TextView, count, true); 
        }

        // <see cref="TextPointer.GetCharacterRect"/>
        Rect ITextPointer.GetCharacterRect(LogicalDirection direction) 
        {
            return TextPointerBase.GetCharacterRect(this, direction); 
        } 

        bool ITextPointer.MoveToInsertionPosition(LogicalDirection direction) 
        {
            return TextPointerBase.MoveToInsertionPosition(this, direction);
        }
 
        bool ITextPointer.MoveToNextInsertionPosition(LogicalDirection direction)
        { 
            return TextPointerBase.MoveToNextInsertionPosition(this, direction); 
        }
 
        //
        // TextContainer modification methods. Disabled by design.
        //
        // This is readonly Text OM. All modification methods returns false 
        //
 
        /// <summary> 
        /// </summary>
        void ITextPointer.InsertTextInRun(string textData) 
        {
            if (textData == null)
            {
                throw new ArgumentNullException("textData"); 
            }
 
            throw new InvalidOperationException(SR.Get(SRID.FixedDocumentReadonly)); 
        }
 
        /// <summary>
        /// </summary>
        void ITextPointer.DeleteContentToPosition(ITextPointer limit)
        { 
            throw new InvalidOperationException(SR.Get(SRID.FixedDocumentReadonly));
        } 
 
        /// <see cref="ITextPointer.ValidateLayout"/>
        bool ITextPointer.ValidateLayout() 
        {
            return TextPointerBase.ValidateLayout(this, ((ITextPointer)this).TextContainer.TextView);
        }
 
        #endregion TextNavigator Methods
 
        //------------------------------------------------------ 
        //
        //  Public Properties 
        //
        //------------------------------------------------------

        #region TextPointer Properties 

        // <see cref="System.Windows.Documents.ITextPointer.ParentType"/> 
        Type ITextPointer.ParentType 
        {
            get 
            {
                FixedElement e = _flowPosition.GetScopingElement();
                return e.IsTextElement ? e.Type : ((ITextContainer)_flowPosition.TextContainer).Parent.GetType();
            } 
        }
 
        /// <summary> 
        /// <see cref="TextPointer.TextContainer"/>
        /// </summary> 
        ITextContainer ITextPointer.TextContainer
        {
            get { return this.FixedTextContainer; }
        } 

        // <see cref="TextPointer.HasValidLayout"/> 
        bool ITextPointer.HasValidLayout 
        {
            get 
            {
                return (((ITextPointer)this).TextContainer.TextView != null &&
                        ((ITextPointer)this).TextContainer.TextView.IsValid &&
                        ((ITextPointer)this).TextContainer.TextView.Contains(this)); 
            }
        } 
 
        // <see cref="ITextPointer.IsAtCaretUnitBoundary"/>
        bool ITextPointer.IsAtCaretUnitBoundary 
        {
            get
            {
                Invariant.Assert(((ITextPointer)this).HasValidLayout); 
                ITextView textView = ((ITextPointer)this).TextContainer.TextView;
                bool isAtCaretUnitBoundary = textView.IsAtCaretUnitBoundary(this); 
 
                if (!isAtCaretUnitBoundary && this.LogicalDirection == LogicalDirection.Backward)
                { 
                    // In MIL Text and TextView worlds, a position at trailing edge of a newline (with backward gravity)
                    // is not an allowed caret stop.
                    // However, in TextPointer world we must allow such a position to be a valid insertion position,
                    // since it breaks textrange normalization for empty ranges. 
                    // Hence, we need to check for TextView.IsAtCaretUnitBoundary in reverse direction below.
 
                    ITextPointer positionForwardGravity = ((ITextPointer)this).CreatePointer(LogicalDirection.Forward); 
                    isAtCaretUnitBoundary = textView.IsAtCaretUnitBoundary(positionForwardGravity);
                } 
                return isAtCaretUnitBoundary;
            }
        }
 
        /// <summary>
        /// <see cref="TextPointer.LogicalDirection"/> 
        /// </summary> 
        LogicalDirection ITextPointer.LogicalDirection
        { 
            get
            {
                return this.LogicalDirection;
            } 
        }
 
        bool ITextPointer.IsAtInsertionPosition 
        {
            get { return TextPointerBase.IsAtInsertionPosition(this); } 
        }

        // <see cref="TextPointer.IsFrozen"/>
        bool ITextPointer.IsFrozen 
        {
            get 
            { 
                return _isFrozen;
            } 
        }

        // <see cref="ITextPointer.Offset"/>
        int ITextPointer.Offset 
        {
            get 
            { 
                return TextPointerBase.GetOffset(this);
            } 
        }

        // Not implemented.
        int ITextPointer.CharOffset 
        {
            get 
            { 
                #pragma warning suppress 56503
                throw new NotImplementedException(); 
            }
        }

        #endregion TextPointer Properties 

        //----------------------------------------------------- 
        // 
        //  Internal Property
        // 
        //------------------------------------------------------
        #region Internal Property

        internal FlowPosition FlowPosition 
        {
            get 
            { 
                return _flowPosition;
            } 
        }

        internal FixedTextContainer FixedTextContainer
        { 
            get
            { 
                return _flowPosition.TextContainer; 
            }
        } 

        internal LogicalDirection LogicalDirection
        {
            get 
            {
                return _gravity; 
            } 

            set 
            {
                ValidationHelper.VerifyDirection(value, "value");
                Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!");
                _flowPosition = _flowPosition.GetClingPosition(value); 
                _gravity = value;
            } 
        } 

#if DEBUG 
        internal uint DebugId
        {
            get
            { 
                return _debugId;
            } 
        } 
#endif
        #endregion Internal Property 

        //-----------------------------------------------------
        //
        //  Private Fields 
        //
        //----------------------------------------------------- 
        #region Private Fields 
        private LogicalDirection _gravity;
        private FlowPosition     _flowPosition;              // FlowPosition in the content flow 

        // True if Freeze has been called, in which case
        // this TextPointer is immutable and may not be repositioned.
        private bool _isFrozen; 

#if DEBUG 
        private uint    _debugId = (_debugIdCounter++); 
        private static uint _debugIdCounter = 0;
#endif 
        #endregion Private Fields
    }
}

