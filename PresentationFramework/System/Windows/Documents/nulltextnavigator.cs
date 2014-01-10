//---------------------------------------------------------------------------- 
//
// File: NullTextPointer.cs
//
// Copyright (C) 2004 by Microsoft Corporation.  All rights reserved. 
//
// Description: 
//      TextNavigator implementation for NullTextContainer 
//      This is primarily used by internal code.
// 
//---------------------------------------------------------------------------

#pragma warning disable 1634, 1691 // To enable presharp warning disables (#pragma suppress) below.
 
namespace System.Windows.Documents
{ 
    using System; 
    using System.Diagnostics;
    using System.Windows; 
    using MS.Internal;

    /// <summary>
    /// NullTextPointer is an implementation of ITextPointer for NullTextContainer 
    /// </summary>
    internal sealed class NullTextPointer : ITextPointer 
    { 
        //-----------------------------------------------------
        // 
        //  Constructors
        //
        //-----------------------------------------------------
        #region Constructors 
        // Ctor always set mutable flag to false
        internal NullTextPointer(NullTextContainer container, LogicalDirection gravity) 
        { 
            _container = container;
            _gravity = gravity; 
        }
        #endregion Constructors

        //------------------------------------------------------ 
        //
        //  Public Methods 
        // 
        //-----------------------------------------------------
        #region ITextPointer Methods 
        /// <summary>
        /// <see cref="TextPointer.CompareTo"/>
        /// </summary>
        int ITextPointer.CompareTo(ITextPointer position) 
        {
            Debug.Assert(position is NullTextPointer || position is NullTextPointer); 
            // There is single position in the container. 
            return 0;
        } 

        int ITextPointer.CompareTo(StaticTextPointer position)
        {
            // There is single position in the container. 
            return 0;
        } 
 
        /// <summary>
        /// <see cref="TextPointer.GetOffsetToPosition"/> 
        /// </summary>
        int ITextPointer.GetOffsetToPosition(ITextPointer position)
        {
            Debug.Assert(position is NullTextPointer || position is NullTextPointer); 
            // There is single position in the container.
            return 0; 
        } 

        /// <summary> 
        /// <see cref="TextPointer.GetPointerContext"/>
        /// </summary>
        TextPointerContext ITextPointer.GetPointerContext(LogicalDirection direction)
        { 
            // There is no content for this container
            return TextPointerContext.None; 
        } 

        /// <summary> 
        /// <see cref="TextPointer.GetTextRunLength"/>
        /// </summary>
        /// <remarks>Return 0 if non-text run</remarks>
        int ITextPointer.GetTextRunLength(LogicalDirection direction) 
        {
            // There is no content in this container 
            return 0; 
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
            // There is no content in this container. 
            return 0;
        } 
 
        /// <summary>
        /// <see cref="TextPointer.GetAdjacentElement"/> 
        /// </summary>
        /// <remarks>Return null if the embedded object does not exist</remarks>
        object ITextPointer.GetAdjacentElement(LogicalDirection direction)
        { 
            // There is no content in this container.
            return null; 
        } 

        /// <summary> 
        /// <see cref="TextPointer.GetElementType"/>
        /// </summary>
        /// <remarks>Return null if no TextElement in the direction</remarks>
        Type ITextPointer.GetElementType(LogicalDirection direction) 
        {
            // There is no content in this container. 
            return null; 
        }
 
        /// <summary>
        /// <see cref="TextPointer.HasEqualScope"/>
        /// </summary>
        bool ITextPointer.HasEqualScope(ITextPointer position) 
        {
            return true; 
        } 

        /// <summary> 
        /// <see cref="TextPointer.GetValue"/>
        /// </summary>
        /// <remarks>return property values even if there is no scoping element</remarks>
        object ITextPointer.GetValue(DependencyProperty property) 
        {
            return property.DefaultMetadata.DefaultValue; 
        } 

        /// <summary> 
        /// <see cref="TextPointer.ReadLocalValue"/>
        /// </summary>
        object ITextPointer.ReadLocalValue(DependencyProperty property)
        { 
            return DependencyProperty.UnsetValue;
        } 
 
        /// <summary>
        /// <see cref="TextPointer.GetLocalValueEnumerator"/> 
        /// </summary>
        /// <remarks>Returns an empty enumerator if there is no scoping element</remarks>
        LocalValueEnumerator ITextPointer.GetLocalValueEnumerator()
        { 
            return (new DependencyObject()).GetLocalValueEnumerator();
        } 
 
        ITextPointer ITextPointer.CreatePointer()
        { 
            return ((ITextPointer)this).CreatePointer(0, _gravity);
        }

        // Unoptimized CreateStaticPointer implementation. 
        // Creates a simple wrapper for an ITextPointer instance.
        StaticTextPointer ITextPointer.CreateStaticPointer() 
        { 
            return new StaticTextPointer(((ITextPointer)this).TextContainer, ((ITextPointer)this).CreatePointer());
        } 

        ITextPointer ITextPointer.CreatePointer(int distance)
        {
            return ((ITextPointer)this).CreatePointer(distance, _gravity); 
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
            // There is no content in this container
            Debug.Assert(distance == 0); 
            return new NullTextPointer(_container, gravity);
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

        #endregion ITextPointer Methods 

        #region ITextPointer Methods

        /// <summary> 
        /// <see cref="ITextPointer.SetLogicalDirection"/>
        /// </summary> 
        /// <param name="direction"></param> 
        void ITextPointer.SetLogicalDirection(LogicalDirection direction)
        { 
            ValidationHelper.VerifyDirection(direction, "gravity");
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!");

            _gravity = direction; 
        }
 
        /// <summary> 
        /// <see cref="TextPointer.MoveByOffset"/>
        /// </summary> 
        bool ITextPointer.MoveToNextContextPosition(LogicalDirection direction)
        {
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!");
 
            // Nowhere to move in an empty container
            return false; 
        } 

 
        /// <summary>
        /// <see cref="TextPointer.MoveByOffset"/>
        /// </summary>
        int ITextPointer.MoveByOffset(int distance) 
        {
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!"); 
 
            Debug.Assert(distance == 0, "Single possible position in this empty container");
 
            return 0;
        }

        /// <summary> 
        /// <see cref="TextPointer.MoveToPosition"/>
        /// </summary> 
        void ITextPointer.MoveToPosition(ITextPointer position) 
        {
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!"); 

            // There is single possible position in this empty container.
        }
 
        /// <summary>
        /// <see cref="TextPointer.MoveToElementEdge"/> 
        /// </summary> 
        void ITextPointer.MoveToElementEdge(ElementEdge edge)
        { 
            Debug.Assert(!_isFrozen, "Can't reposition a frozen pointer!");

            Debug.Assert(false, "No scoping element!");
        } 

        // <see cref="TextPointer.MoveToLineBoundary"/> 
        int ITextPointer.MoveToLineBoundary(int count) 
        {
            Debug.Assert(false, "NullTextPointer does not expect layout dependent method calls!"); 
            return 0;
        }

        // <see cref="TextPointer.GetCharacterRect"/> 
        Rect ITextPointer.GetCharacterRect(LogicalDirection direction)
        { 
            Debug.Assert(false, "NullTextPointer does not expect layout dependent method calls!"); 
            return new Rect();
        } 

        bool ITextPointer.MoveToInsertionPosition(LogicalDirection direction)
        {
            return TextPointerBase.MoveToInsertionPosition(this, direction); 
        }
 
        bool ITextPointer.MoveToNextInsertionPosition(LogicalDirection direction) 
        {
            return TextPointerBase.MoveToNextInsertionPosition(this, direction); 
        }

        void ITextPointer.InsertTextInRun(string textData)
        { 
            Debug.Assert(false); // must never call this
        } 
 
        void ITextPointer.DeleteContentToPosition(ITextPointer limit)
        { 
            Debug.Assert(false); // must never call this
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

        /// <see cref="ITextPointer.ValidateLayout"/> 
        bool ITextPointer.ValidateLayout()
        {
            return false;
        } 

        #endregion ITextPointer Methods 
 

        //------------------------------------------------------ 
        //
        //  Public Properties
        //
        //------------------------------------------------------ 

        #region ITextPointer Properties 
 
        // <see cref="System.Windows.Documents.ITextPointer.ParentType"/>
        Type ITextPointer.ParentType 
        {
            get
            {
                // There is no content in this container. 
                // We want to behave consistently with FixedTextPointer so we know
                // we're at the root when the parent is a FixedDocument 
                return typeof(FixedDocument); 
            }
        } 

        /// <summary>
        /// <see cref="TextPointer.TextContainer"/>
        /// </summary> 
        ITextContainer ITextPointer.TextContainer
        { 
            get { return _container; } 
        }
 
        // <see cref="TextPointer.HasValidLayout"/>
        bool ITextPointer.HasValidLayout
        {
            get 
            {
                // NullTextContainer's never have a layout. 
                return false; 
            }
        } 

        // <see cref="ITextPointer.IsAtCaretUnitBoundary"/>
        bool ITextPointer.IsAtCaretUnitBoundary
        { 
            get
            { 
                Invariant.Assert(false, "NullTextPointer never has valid layout!"); 
                return false;
            } 
        }

        /// <summary>
        /// <see cref="TextPointer.LogicalDirection"/> 
        /// </summary>
        LogicalDirection ITextPointer.LogicalDirection 
        { 
            get { return _gravity; }
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
                return 0;
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
 
        #endregion ITextPointer Properties
 
        //-----------------------------------------------------
        //
        //  Private Fields
        // 
        //------------------------------------------------------
        #region Private Fields 
        private LogicalDirection    _gravity; 
        private NullTextContainer   _container;
 
        // True if Freeze has been called, in which case
        // this TextPointer is immutable and may not be repositioned.
        private bool _isFrozen;
 
        #endregion Private Fields
    } 
} 


