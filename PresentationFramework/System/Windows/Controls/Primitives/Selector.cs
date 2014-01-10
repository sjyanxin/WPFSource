//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//--------------------------------------------------------------------------- 

using System.ComponentModel; 
using System.Collections; 
using System.Collections.Generic;
using System.Collections.ObjectModel; 
using System.Collections.Specialized;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows; 
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider; 
using System.Windows.Input; 
using System.Xml;
using MS.Utility; 
using MS.Internal;
using MS.Internal.Data;
using MS.Internal.KnownBoxes;
using MS.Internal.Hashing.PresentationFramework;    // HashHelper 

using System; 
using System.Diagnostics; 
using MS.Internal.Controls;
 
// Disable CS3001: Warning as Error: not CLS-compliant
#pragma warning disable 3001

namespace System.Windows.Controls.Primitives 
{
    /// <summary> 
    /// The base class for controls that select items from among their children 
    /// </summary>
    [DefaultEvent("SelectionChanged"), DefaultProperty("SelectedIndex")] 
    [Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)] // cannot be read & localized as string
    public abstract class Selector : ItemsControl
    {
        //------------------------------------------------------------------- 
        //
        //  Constructors 
        // 
        //-------------------------------------------------------------------
 
        #region Constructors

        /// <summary>
        ///     Default Selector constructor. 
        /// </summary>
        protected Selector() : base() 
        { 
            Items.CurrentChanged += new EventHandler(OnCurrentChanged);
            ItemContainerGenerator.StatusChanged += new EventHandler(OnGeneratorStatusChanged); 

            _focusEnterMainFocusScopeEventHandler = new EventHandler(OnFocusEnterMainFocusScope);
            KeyboardNavigation.Current.FocusEnterMainFocusScope += _focusEnterMainFocusScopeEventHandler;
 
            ObservableCollection<object> selectedItems = new SelectedItemCollection(this);
            SetValue(SelectedItemsPropertyKey, selectedItems); 
            selectedItems.CollectionChanged += new NotifyCollectionChangedEventHandler(OnSelectedItemsCollectionChanged); 

            // to prevent this inherited property from bleeding into nested selectors, set this locally to 
            // false at construction time
            SetValue(IsSelectionActivePropertyKey, BooleanBoxes.FalseBox);
        }
 
        static Selector()
        { 
            EventManager.RegisterClassHandler(typeof(Selector), Selector.SelectedEvent, new RoutedEventHandler(Selector.OnSelected)); 
            EventManager.RegisterClassHandler(typeof(Selector), Selector.UnselectedEvent, new RoutedEventHandler(Selector.OnUnselected));
        } 

        #endregion

        //-------------------------------------------------------------------- 
        //
        //  Public Events 
        // 
        //-------------------------------------------------------------------
 
        #region Public Events

        /// <summary>
        ///     An event fired when the selection changes. 
        /// </summary>
        public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent( 
            "SelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(Selector)); 

        /// <summary> 
        ///     An event fired when the selection changes.
        /// </summary>
        [Category("Behavior")]
        public event SelectionChangedEventHandler SelectionChanged 
        {
            add { AddHandler(SelectionChangedEvent, value); } 
            remove { RemoveHandler(SelectionChangedEvent, value); } 
        }
 
        /// <summary>
        ///     An event fired by UI children when the IsSelected property changes to true.
        ///     For listening to selection state changes use <see cref="SelectionChangedEvent" /> instead.
        /// </summary> 
        public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent(
            "Selected", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Selector)); 
 
        /// <summary>
        ///     Adds a handler for the SelectedEvent attached event 
        ///     For listening to selection state changes use <see cref="SelectionChangedEvent" /> instead.
        /// </summary>
        /// <param name="element">UIElement or ContentElement that listens to this event</param>
        /// <param name="handler">Event Handler to be added</param> 
        public static void AddSelectedHandler(DependencyObject element, RoutedEventHandler handler)
        { 
            FrameworkElement.AddHandler(element, SelectedEvent, handler); 
        }
 
        /// <summary>
        ///     Removes a handler for the SelectedEvent attached event
        ///     For listening to selection state changes use <see cref="SelectionChangedEvent" /> instead.
        /// </summary> 
        /// <param name="element">UIElement or ContentElement that listens to this event</param>
        /// <param name="handler">Event Handler to be removed</param> 
        public static void RemoveSelectedHandler(DependencyObject element, RoutedEventHandler handler) 
        {
            FrameworkElement.RemoveHandler(element, SelectedEvent, handler); 
        }

        /// <summary>
        ///     An event fired by UI children when the IsSelected property changes to false. 
        ///     For listening to selection state changes use <see cref="SelectionChangedEvent" /> instead.
        /// </summary> 
        public static readonly RoutedEvent UnselectedEvent = EventManager.RegisterRoutedEvent( 
            "Unselected", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Selector));
 
        /// <summary>
        ///     Adds a handler for the UnselectedEvent attached event
        ///     For listening to selection state changes use <see cref="SelectionChangedEvent" /> instead.
        /// </summary> 
        /// <param name="element">UIElement or ContentElement that listens to this event</param>
        /// <param name="handler">Event Handler to be added</param> 
        public static void AddUnselectedHandler(DependencyObject element, RoutedEventHandler handler) 
        {
            FrameworkElement.AddHandler(element, UnselectedEvent, handler); 
        }

        /// <summary>
        ///     Removes a handler for the UnselectedEvent attached event 
        ///     For listening to selection state changes use <see cref="SelectionChangedEvent" /> instead.
        /// </summary> 
        /// <param name="element">UIElement or ContentElement that listens to this event</param> 
        /// <param name="handler">Event Handler to be removed</param>
        public static void RemoveUnselectedHandler(DependencyObject element, RoutedEventHandler handler) 
        {
            FrameworkElement.RemoveHandler(element, UnselectedEvent, handler);
        }
 
        #endregion
 
        //-------------------------------------------------------------------- 
        //
        //  Public Properties 
        //
        //--------------------------------------------------------------------

        #region Public Properties 

        // ----------------------------------------------------------------- 
        //  Attached Properties 
        // ------------------------------------------------------------------
 
        /// <summary>
        ///     Property key for IsSelectionActiveProperty.
        /// </summary>
        internal static readonly DependencyPropertyKey IsSelectionActivePropertyKey = 
                DependencyProperty.RegisterAttachedReadOnly(
                        "IsSelectionActive", 
                        typeof(bool), 
                        typeof(Selector),
                        new FrameworkPropertyMetadata(BooleanBoxes.FalseBox, FrameworkPropertyMetadataOptions.Inherits)); 

        /// <summary>
        ///     Indicates whether the keyboard focus is within the Selector.
        /// In case when focus goes to Menu/Toolbar then selection is active too. 
        /// </summary>
        public static readonly DependencyProperty IsSelectionActiveProperty = 
            IsSelectionActivePropertyKey.DependencyProperty; 

        /// <summary> 
        ///     Get IsSelectionActive property
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns> 
        public static bool GetIsSelectionActive(DependencyObject element)
        { 
            if (element == null) 
            {
                throw new ArgumentNullException("element"); 
            }
            return (bool) element.GetValue(IsSelectionActiveProperty);
        }
 
        /// <summary>
        ///     Specifies whether a UI container for an item in a Selector should appear selected. 
        /// </summary> 
        public static readonly DependencyProperty IsSelectedProperty =
                DependencyProperty.RegisterAttached( 
                        "IsSelected",
                        typeof(bool),
                        typeof(Selector),
                        new FrameworkPropertyMetadata( 
                                BooleanBoxes.FalseBox,
                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)); 
 
        /// <summary>
        ///     Retrieves the value of the attached property. 
        /// </summary>
        /// <param name="element">The DependencyObject on which to query the property.</param>
        /// <returns>The value of the attached property.</returns>
        [AttachedPropertyBrowsableForChildren()] 
        public static bool GetIsSelected(DependencyObject element)
        { 
            if (element == null) 
            {
                throw new ArgumentNullException("element"); 
            }

            return (bool) element.GetValue(IsSelectedProperty);
        } 

 
        /// <summary> 
        ///     Sets the value of the attached property.
        /// </summary> 
        /// <param name="element">The DependencyObject on which to set the property.</param>
        /// <param name="isSelected">The new value of the attached property.</param>
        public static void SetIsSelected(DependencyObject element, bool isSelected)
        { 
            if (element == null)
            { 
                throw new ArgumentNullException("element"); 
            }
 
            element.SetValue(IsSelectedProperty, BooleanBoxes.Box(isSelected));
        }

        // ----------------------------------------------------------------- 
        //  Direct Properties
        // ----------------------------------------------------------------- 
 
        /// <summary>
        /// Whether this Selector should keep SelectedItem in [....] with the ItemCollection's current item. 
        /// </summary>
        public static readonly DependencyProperty IsSynchronizedWithCurrentItemProperty =
                DependencyProperty.Register(
                        "IsSynchronizedWithCurrentItem", 
                        typeof(bool?),
                        typeof(Selector), 
                        new FrameworkPropertyMetadata( 
                                (bool?)null,
                                new PropertyChangedCallback(OnIsSynchronizedWithCurrentItemChanged))); 

        /// <summary>
        /// Whether this Selector should keep SelectedItem in [....] with the ItemCollection's current item.
        /// </summary> 
        [Bindable(true), Category("Behavior")]
        [TypeConverter("System.Windows.NullableBoolConverter, PresentationFramework, Version=" + Microsoft.Internal.BuildInfo.WCP_VERSION + ", Culture=neutral, PublicKeyToken=" + Microsoft.Internal.BuildInfo.WCP_PUBLIC_KEY_TOKEN + ", Custom=null")] 
        [Localizability(LocalizationCategory.NeverLocalize)] // not localizable 
        public bool? IsSynchronizedWithCurrentItem
        { 
            get { return (bool?) GetValue(IsSynchronizedWithCurrentItemProperty); }
            set { SetValue(IsSynchronizedWithCurrentItemProperty, value); }
        }
 
        private static void OnIsSynchronizedWithCurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
            Selector s = (Selector)d; 
            s.SetSynchronizationWithCurrentItem();
        } 

        private void SetSynchronizationWithCurrentItem()
        {
            bool? isSynchronizedWithCurrentItem = IsSynchronizedWithCurrentItem; 
            bool oldSync = IsSynchronizedWithCurrentItemPrivate;
            bool newSync; 
 
            if (isSynchronizedWithCurrentItem.HasValue)
            { 
                // if there's a value, use it
                newSync = isSynchronizedWithCurrentItem.Value;
            }
            else 
            {
                // don't do the default logic until the end of initialization. 
                // This reduces the dependence on the order of property-setting. 
                if (!IsInitialized)
                    return; 

                // when the value is null, synchronize iff selection mode is Single
                // and there's a non-default view.
                SelectionMode mode = (SelectionMode)GetValue(ListBox.SelectionModeProperty); 
                newSync = (mode == SelectionMode.Single) &&
                            !CollectionViewSource.IsDefaultView(Items.CollectionView); 
            } 

            IsSynchronizedWithCurrentItemPrivate = newSync; 

            if (!oldSync && newSync)
            {
                // if the selection has already been set, honor it and bring currency 
                // into [....].  (Typical case:  <ListBox SelectedItem=x IsSync=true/>)
                // Otherwise, bring selection into [....] with currency. 
                if (SelectedItem != null) 
                {
                    SetCurrentToSelected(); 
                }
                else
                {
                    SetSelectedToCurrent(); 
                }
            } 
        } 

        /// <summary> 
        ///     SelectedIndex DependencyProperty
        /// </summary>
        public static readonly DependencyProperty SelectedIndexProperty =
                DependencyProperty.Register( 
                        "SelectedIndex",
                        typeof(int), 
                        typeof(Selector), 
                        new FrameworkPropertyMetadata(
                                -1, 
                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                                new PropertyChangedCallback(OnSelectedIndexChanged),
                                new CoerceValueCallback(CoerceSelectedIndex)),
                        new ValidateValueCallback(ValidateSelectedIndex)); 

        /// <summary> 
        ///     The index of the first item in the current selection or -1 if the selection is empty. 
        /// </summary>
        [Bindable(true), Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        [Localizability(LocalizationCategory.NeverLocalize)] // not localizable
        public int SelectedIndex
        {
            get { return (int) GetValue(SelectedIndexProperty); } 
            set { SetValue(SelectedIndexProperty, value); }
        } 
 
        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
            Selector s = (Selector) d;

            // If we're in the middle of a selection change, ignore all changes
            if (!s.SelectionChange.IsActive) 
            {
                int newIndex = (int) e.NewValue; 
                object item = (newIndex == -1) ? null : s.Items[newIndex]; 
                s.SelectionChange.SelectJustThisItem(item, true /* assumeInItemsCollection */);
            } 
        }

        private static bool ValidateSelectedIndex(object o)
        { 
            return ((int) o) >= -1;
        } 
 
        private static object CoerceSelectedIndex(DependencyObject d, object value)
        { 
            Selector s = (Selector) d;
            if ((value is int) && (int) value >= s.Items.Count)
            {
                return DependencyProperty.UnsetValue; 
            }
 
            return value; 
        }
 


        /// <summary>
        ///     SelectedItem DependencyProperty 
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty = 
                DependencyProperty.Register( 
                        "SelectedItem",
                        typeof(object), 
                        typeof(Selector),
                        new FrameworkPropertyMetadata(
                                null,
                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, 
                                new PropertyChangedCallback(OnSelectedItemChanged),
                                new CoerceValueCallback(CoerceSelectedItem))); 
 
        /// <summary>
        ///  The first item in the current selection, or null if the selection is empty. 
        /// </summary>
        [Bindable(true), Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SelectedItem
        { 
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); } 
        } 

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        {
            Selector s = (Selector) d;

            if (!s.SelectionChange.IsActive) 
            {
                s.SelectionChange.SelectJustThisItem(e.NewValue, false /* assumeInItemsCollection */); 
            } 
        }
 
        private static object CoerceSelectedItem(DependencyObject d, object value)
        {
            Selector s = (Selector) d;
            if (value == null || s.SkipCoerceSelectedItemCheck) 
                 return value;
 
            int selectedIndex = s.SelectedIndex; 

            if ( (selectedIndex > -1 && selectedIndex < s.Items.Count && s.Items[selectedIndex] == value) 
                || s.Items.Contains(value))
            {
                return value;
            } 

            return DependencyProperty.UnsetValue; 
        } 

 

        /// <summary>
        ///     SelectedValue DependencyProperty
        /// </summary> 
        public static readonly DependencyProperty SelectedValueProperty =
                DependencyProperty.Register( 
                        "SelectedValue", 
                        typeof(object),
                        typeof(Selector), 
                        new FrameworkPropertyMetadata(
                                null,
                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                                new PropertyChangedCallback(OnSelectedValueChanged), 
                                new CoerceValueCallback(CoerceSelectedValue)));
 
        /// <summary> 
        ///  The value of the SelectedItem, obtained using the SelectedValuePath.
        /// </summary> 
        /// <remarks>
        /// <p>Setting SelectedValue to some value x attempts to select an item whose
        /// "value" evaluates to x, using the current setting of <seealso cref="SelectedValuePath"/>.
        /// If no such item can be found, the selection is cleared.</p> 
        ///
        /// <p>Getting the value of SelectedValue returns the "value" of the <seealso cref="SelectedItem"/>, 
        /// using the current setting of <seealso cref="SelectedValuePath"/>, or null 
        /// if there is no selection.</p>
        /// 
        /// <p>Note that these rules imply that getting SelectedValue immediately after
        /// setting it to x will not necessarily return x.  It might return null,
        /// if no item with value x can be found.</p>
        /// </remarks> 
        [Bindable(true), Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Localizability(LocalizationCategory.NeverLocalize)] // not localizable 
        public object SelectedValue 
        {
            get { return GetValue(SelectedValueProperty); } 
            set { SetValue(SelectedValueProperty, value); }
        }

        /// <summary> 
        /// This could happen when SelectedValuePath has changed,
        /// SelectedItem has changed, or someone is setting SelectedValue. 
        /// </summary> 
        private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
        }

        // Select an item whose value matches the given value
        private object SelectItemWithValue(object value) 
        {
            _cacheValid[(int)CacheBits.SelectedValueDrivesSelection] = true; 
 
            // look through the items for one whose value matches the given value
            object item = FindItemWithValue(value); 

            // We can assume it's in the collection because we just searched
            // through the collection to find it.
            SelectionChange.SelectJustThisItem(item, true /* assumeInItemsCollection */); 

            // if there are no items, protect SelectedValue from being overwritten 
            // until items show up.  This enables a SelectedValue set from markup 
            // to set the initial selection when the items eventually appear.
            // Otherwise, allow SelectedValue to follow SelectedItem. 
            if (item == DependencyProperty.UnsetValue && !HasItems)
            {
                _cacheValid[(int)CacheBits.SelectedValueWaitsForItems] = true;
            } 

            _cacheValid[(int)CacheBits.SelectedValueDrivesSelection] = false; 
            return item; 
        }
 
        private object FindItemWithValue(object value)
        {
            if (!HasItems)
                return DependencyProperty.UnsetValue; 

            // use a representative item to determine which kind of binding to use (XML vs. CLR) 
            BindingExpression bindingExpr = PrepareItemValueBinding(Items.GetRepresentativeItem()); 

            if (bindingExpr == null) 
                return DependencyProperty.UnsetValue;   // no suitable item found

            // optimize for case where there is no SelectedValuePath (meaning
            // that the value of the item is the item itself, or the InnerText 
            // of the item)
            if (string.IsNullOrEmpty(SelectedValuePath)) 
            { 
                // when there's no SelectedValuePath, the binding's Path
                // is either empty (CLR) or "/InnerText" (XML) 
                string path = bindingExpr.ParentBinding.Path.Path;
                Debug.Assert(String.IsNullOrEmpty(path) || path == "/InnerText");
                if (string.IsNullOrEmpty(path))
                { 
                    // CLR - item is its own selected value
                    if (Items.Contains(value)) 
                        return value; 
                    else
                        return DependencyProperty.UnsetValue; 
                }
                else
                {
                    // XML - use the InnerText as the selected value 
                    return FindXmlNodeWithInnerText(value);
                } 
            } 

            Type selectedType = (value != null) ?  value.GetType() : null; 
            object selectedValue = value;
            DynamicValueConverter converter = new DynamicValueConverter(false);

            foreach (object current in Items) 
            {
                bindingExpr.Activate(current); 
                object itemValue = bindingExpr.Value; 
                if (VerifyEqual(value, selectedType, itemValue, converter))
                { 
                    bindingExpr.Deactivate();
                    return current;
                }
            } 
            bindingExpr.Deactivate();
 
            return DependencyProperty.UnsetValue; 
        }
 
        private bool VerifyEqual(object knownValue, Type knownType, object itemValue, DynamicValueConverter converter)
        {
            object tempValue = knownValue;
 
            if (knownType != null && itemValue != null)
            { 
                Type itemType = itemValue.GetType(); 

                // determine if selectedValue is comparable to itemValue, convert if necessary 
                // using a DefaultValueConverter
                if (!knownType.IsAssignableFrom(itemType))
                {
                    tempValue = converter.Convert(knownValue, itemType); 
                    if (tempValue == DependencyProperty.UnsetValue)
                    { 
                        // can't convert, keep original value for the following object comparison 
                        tempValue = knownValue;
                    } 
                }
            }

            return Object.Equals(tempValue, itemValue); 
        }
 
        // separate function to avoid loading System.Xml until we have a good reason 
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private object FindXmlNodeWithInnerText(object innerText) 
        {
            string innerTextString = innerText as string;

            if (innerTextString != null) 
            {
                foreach (object item in Items) 
                { 
                    XmlNode node = item as XmlNode;
                    if (node != null && node.InnerText == innerTextString) 
                        return node;
                }
            }
 
            return DependencyProperty.UnsetValue;
        } 
 

        private static object CoerceSelectedValue(DependencyObject d, object value) 
        {
            Selector s = (Selector)d;

            if (s.SelectionChange.IsActive) 
            {
                // If we're in the middle of a selection change, accept the value 
                s._cacheValid[(int)CacheBits.SelectedValueDrivesSelection] = false; 
            }
            else 
            {
                // Otherwise, this is a user-initiated change to SelectedValue.
                // Find the corresponding item.
                object item = s.SelectItemWithValue(value); 

                // if the search fails, coerce the value to null.  Unless there 
                // are no items at all, in which case wait for the items to appear 
                // and search again.
                if (item == DependencyProperty.UnsetValue && s.HasItems) 
                {
                    value = null;
                }
            } 

            return value; 
        } 

 
        /// <summary>
        ///     SelectedValuePath DependencyProperty
        /// </summary>
        public static readonly DependencyProperty SelectedValuePathProperty = 
                DependencyProperty.Register(
                        "SelectedValuePath", 
                        typeof(string), 
                        typeof(Selector),
                        new FrameworkPropertyMetadata( 
                                String.Empty,
                                new PropertyChangedCallback(OnSelectedValuePathChanged)));

        /// <summary> 
        ///  The path used to retrieve the SelectedValue from the SelectedItem
        /// </summary> 
        [Bindable(true), Category("Appearance")] 
        [Localizability(LocalizationCategory.NeverLocalize)] // not localizable
        public string SelectedValuePath 
        {
            get { return (string) GetValue(SelectedValuePathProperty); }
            set { SetValue(SelectedValuePathProperty, value); }
        } 

        private static void OnSelectedValuePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        { 
            Selector s = (Selector)d;
            // discard the current ItemValue binding 
            ItemValueBindingExpression.ClearValue(s);

            // select the corresponding item
            EffectiveValueEntry entry = s.GetValueEntry( 
                        s.LookupEntry(SelectedValueProperty.GlobalIndex),
                        SelectedValueProperty, 
                        null, 
                        RequestFlags.RawEntry);
            if (entry.IsCoerced || s.SelectedValue != null) 
            {
                // Coercing SelectedValue will retry a previously-set value that had
                // been coerced to null.  (Dev10 513711)
                s.CoerceValue(SelectedValueProperty); 
            }
        } 
 
        /// <summary>
        /// Prepare the binding on the ItemValue property, creating it if necessary. 
        /// Use the item to decide what kind of binding (XML vs. CLR) to use.
        /// </summary>
        /// <param name="item"></param>
        private BindingExpression PrepareItemValueBinding(object item) 
        {
            if (item == null) 
                return null; 

            Binding binding; 
            bool useXml = AssemblyHelper.IsXmlNode(item);

            BindingExpression bindingExpr = ItemValueBindingExpression.GetValue(this);
 
            // replace existing binding if it's the wrong kind
            if (bindingExpr != null) 
            { 
                binding = bindingExpr.ParentBinding;
                bool usesXml = (binding.XPath != null); 
                if ((!usesXml && useXml) || (usesXml && !useXml))
                {
                    ItemValueBindingExpression.ClearValue(this);
                    bindingExpr = null; 
                }
            } 
 
            if (bindingExpr == null)
            { 
                // create the binding
                binding = new Binding();

                // Set source to null so binding does not use ambient DataContext 
                binding.Source = null;
 
                if (useXml) 
                {
                    binding.XPath = SelectedValuePath; 
                    binding.Path = new PropertyPath("/InnerText");
                }
                else
                { 
                    binding.Path = new PropertyPath(SelectedValuePath);
                } 
 
                bindingExpr = (BindingExpression)BindingExpression.CreateUntargetedBindingExpression(this, binding);
                ItemValueBindingExpression.SetValue(this, bindingExpr); 
            }

            return bindingExpr;
        } 

 
 
        /// <summary>
        ///     The key needed set a read-only property. 
        /// </summary>
        private static readonly DependencyPropertyKey SelectedItemsPropertyKey =
                DependencyProperty.RegisterReadOnly(
                        "SelectedItems", 
                        typeof(IList),
                        typeof(Selector), 
                        new FrameworkPropertyMetadata( 
                                (IList) null));
 

        /// <summary>
        /// A read-only IList containing the currently selected items
        /// </summary> 
        internal static readonly DependencyProperty SelectedItemsImplProperty =
                SelectedItemsPropertyKey.DependencyProperty; 
 

        /// <summary> 
        /// The currently selected items.
        /// </summary>
        internal IList SelectedItemsImpl
        { 
            get { return (IList)GetValue(SelectedItemsImplProperty); }
        } 
 

        /// <summary> 
        /// Select multiple items.
        /// </summary>
        /// <param name="selectedItems">Collection of items to be selected.</param>
        /// <returns>true if all items have been selected.</returns> 
        internal bool SetSelectedItemsImpl(IEnumerable selectedItems)
        { 
            bool succeeded = false; 

            if (!SelectionChange.IsActive) 
            {
                SelectionChange.Begin();
                SelectionChange.CleanupDeferSelection();
                ObservableCollection<object> oldSelectedItems = (ObservableCollection<object>) GetValue(SelectedItemsImplProperty); 

                try 
                { 
                    // Unselect everything in oldSelectedItems.
                    if (oldSelectedItems != null) 
                    {
                        foreach (object currentlySelectedItem in oldSelectedItems)
                        {
                            SelectionChange.Unselect(currentlySelectedItem); 
                        }
                    } 
 
                    if (selectedItems != null)
                    { 
                        // Make sure that we can select every items.
                        foreach (object item in selectedItems)
                        {
                            if (!SelectionChange.Select(item, false /* assumeInItemsCollection */)) 
                            {
                                SelectionChange.Cancel(); 
                                return false; 
                            }
                        } 
                    }

                    SelectionChange.End();
                    succeeded = true; 
                }
                finally 
                { 
                    if (!succeeded)
                    { 
                        SelectionChange.Cancel();
                    }
                }
            } 

            return succeeded; 
        } 

        private void OnSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) 
        {
            // Ignore selection changes we're causing.
            if (SelectionChange.IsActive)
            { 
                return;
            } 
 
            if (!CanSelectMultiple)
            { 
                throw new InvalidOperationException(SR.Get(SRID.ChangingCollectionNotSupported));
            }

            SelectionChange.Begin(); 
            bool succeeded=false;
            try 
            { 
                // get the affected item
                object item = null; 
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems.Count != 1) 
                            throw new NotSupportedException(SR.Get(SRID.RangeActionsNotSupported));
                        item = e.NewItems[0]; 
                        break; 

                    case NotifyCollectionChangedAction.Remove: 
                        if (e.OldItems.Count != 1)
                            throw new NotSupportedException(SR.Get(SRID.RangeActionsNotSupported));
                        item = e.OldItems[0];
                        break; 

                    case NotifyCollectionChangedAction.Reset: 
                        break; 

                    case NotifyCollectionChangedAction.Replace: 
                        if (e.NewItems.Count != 1 || e.OldItems.Count != 1)
                            throw new NotSupportedException(SR.Get(SRID.RangeActionsNotSupported));
                        break;
 
                    default:
                        throw new NotSupportedException(SR.Get(SRID.UnexpectedCollectionChangeAction, e.Action)); 
                } 

                switch (e.Action) 
                {
                    case NotifyCollectionChangedAction.Add:
                        {
                            SelectionChange.Select(item, false /* assumeInItemsCollection */); 
                            break;
                        } 
                    case NotifyCollectionChangedAction.Remove: 
                        {
                            SelectionChange.Unselect(item); 
                            break;
                        }
                    case NotifyCollectionChangedAction.Reset:
                        { 
                            SelectionChange.CleanupDeferSelection();
                            for (int i = 0; i < _selectedItems.Count; i++) 
                            { 
                                SelectionChange.Unselect(_selectedItems[i]);
                            } 

                            ObservableCollection<object> userSelectedItems = (ObservableCollection<object>)sender;

                            for (int i = 0; i < userSelectedItems.Count; i++) 
                            {
                                SelectionChange.Select(userSelectedItems[i], false /* assumeInItemsCollection */); 
                            } 
                            break;
                        } 
                    case NotifyCollectionChangedAction.Replace:
                        {
                            SelectionChange.Unselect(e.OldItems[0]);
                            SelectionChange.Select(e.NewItems[0], false /* assumeInItemsCollection */); 
                            break;
                        } 
                } 

                SelectionChange.End(); 
                succeeded = true;
            }
            finally
            { 
                if (!succeeded)
                { 
                    SelectionChange.Cancel(); 
                }
            } 
        }

        #endregion
 

        //------------------------------------------------------------------- 
        // 
        //  Internal Properties
        // 
        //--------------------------------------------------------------------

        #region Internal Properties
 
        /// <summary>
        /// Whether this Selector can select more than one item at once 
        /// </summary> 
        internal bool CanSelectMultiple
        { 
            get { return _cacheValid[(int)CacheBits.CanSelectMultiple]; }
            set
            {
                if (_cacheValid[(int)CacheBits.CanSelectMultiple] != value) 
                {
                    _cacheValid[(int)CacheBits.CanSelectMultiple] = value; 
                    if (!value && (_selectedItems.Count > 1)) 
                    {
                        SelectionChange.Validate(); 
                    }
                }
            }
        } 

        #endregion 
 
        //-------------------------------------------------------------------
        // 
        //  Internal Methods
        //
        //--------------------------------------------------------------------
 
        #region Internal Methods
 
        /// <summary> 
        /// Clear the IsSelected property from containers that are no longer used.  This is done for container recycling;
        /// If we ever reuse a container with a stale IsSelected value the UI will incorrectly display it as selected. 
        /// </summary>
        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item); 

            //This check ensures that selection is cleared only when the element is getting recycled. 
            if ( !((IGeneratorHost)this).IsItemItsOwnContainer(item) ) 
            {
                element.ClearValue(IsSelectedProperty); 
            }
        }

        internal void RaiseIsSelectedChangedAutomationEvent(DependencyObject container, bool isSelected) 
        {
            SelectorAutomationPeer selectorPeer = UIElementAutomationPeer.FromElement(this) as SelectorAutomationPeer; 
            if (selectorPeer != null && selectorPeer.ItemPeers != null) 
            {
                object item = GetItemOrContainerFromContainer(container); 
                if (item != null)
                {
                    SelectorItemAutomationPeer itemPeer = selectorPeer.ItemPeers[item] as SelectorItemAutomationPeer;
                    if (itemPeer != null) 
                        itemPeer.RaiseAutomationIsSelectedChanged(isSelected);
                } 
            } 
        }
 
        internal void SetInitialMousePosition()
        {
            _lastMousePosition = Mouse.GetPosition(this);
        } 

        // Tracks mouse movement. 
        // Returns true if the mouse moved from the last time this method was called. 
        internal bool DidMouseMove()
        { 
            Point newPosition = Mouse.GetPosition(this);
            if (newPosition != _lastMousePosition)
            {
                _lastMousePosition = newPosition; 
                return true;
            } 
 
            return false;
        } 

        internal void ResetLastMousePosition()
        {
            _lastMousePosition = new Point(); 
        }
 
        /// <summary> 
        /// Select all items in the collection.
        /// Assumes that CanSelectMultiple is true 
        /// </summary>
        internal virtual void SelectAllImpl()
        {
            Debug.Assert(CanSelectMultiple, "CanSelectMultiple should be true when calling SelectAllImpl"); 

            SelectionChange.Begin(); 
            SelectionChange.CleanupDeferSelection(); 
            try
            { 
                foreach (object current in Items)
                {
                    SelectionChange.Select(current, true /* assumeInItemsCollection */);
                } 
            }
            finally 
            { 
                SelectionChange.End();
            } 
        }

        /// <summary>
        /// Unselect all items in the collection. 
        /// </summary>
        internal virtual void UnselectAllImpl() 
        { 
            SelectionChange.Begin();
            SelectionChange.CleanupDeferSelection(); 
            try
            {
                object selectedItem = InternalSelectedItem;
 
                foreach (object current in _selectedItems)
                { 
                    SelectionChange.Unselect(current); 
                }
            } 
            finally
            {
                SelectionChange.End();
            } 
        }
 
        #endregion 

        //-------------------------------------------------------------------- 
        //
        //  Protected Methods
        //
        //------------------------------------------------------------------- 

        #region Protected Methods 
 
        /// <summary>
        /// Updates the current selection when Items has changed 
        /// </summary>
        /// <param name="e">Information about what has changed</param>
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        { 
            // When items become available, reevaluate the choice of algorithm
            // used by _selectedItems. 
            if (e.Action == NotifyCollectionChangedAction.Reset || 
                (e.Action == NotifyCollectionChangedAction.Add &&
                 e.NewStartingIndex == 0)) 
            {
                ResetSelectedItemsAlgorithm();
            }
 
            base.OnItemsChanged(e);
 
            // Do not coerce the SelectedIndexProperty if it holds a DeferredSelectedIndexReference 
            // because this deferred reference object is guaranteed to produce a pre-coerced value.
            // Also if you did coerce it then you will lose the attempted performance optimization 
            // because it will get dereferenced immediately in order to supply a baseValue for coersion.

            EffectiveValueEntry entry = GetValueEntry(
                        LookupEntry(SelectedIndexProperty.GlobalIndex), 
                        SelectedIndexProperty,
                        null, 
                        RequestFlags.DeferredReferences); 

            if (!entry.IsDeferredReference || 
                !(entry.Value is DeferredSelectedIndexReference))
            {
                CoerceValue(SelectedIndexProperty);
            } 

            CoerceValue(SelectedItemProperty); 
 
            if (_cacheValid[(int)CacheBits.SelectedValueWaitsForItems] &&
                !Object.Equals(SelectedValue, InternalSelectedValue)) 
            {
                // This sets the selection from SelectedValue when SelectedValue
                // was set prior to the arrival of any items to select, provided
                // that SelectedIndex or SelectedItem didn't already do it. 
                SelectItemWithValue(SelectedValue);
            } 
 
            switch (e.Action)
            { 
                case NotifyCollectionChangedAction.Add:
                {
                    SelectionChange.Begin();
                    try 
                    {
                        object element = e.NewItems[0]; 
                        // If we added something, see if it was set be selected and [....]. 
                        if (ItemGetIsSelected(element))
                        { 
                            SelectionChange.Select(element, true /* assumeInItemsCollection */);
                        }
                    }
                    finally 
                    {
                        SelectionChange.End(); 
                    } 
                    break;
                } 
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                {
                    SelectionChange.Begin(); 
                    try
                    { 
                        // if they removed something in a selection, remove it. 
                        // When End() commits the changes it will update SelectedIndex.
                        object element = e.OldItems[0]; 

                        if (_selectedItems.Contains(element))
                        {
                            SelectionChange.Unselect(element); 
                        }
                    } 
                    finally 
                    {
                        // Here SelectedIndex will be fixed to point to the first thing in _selectedItems, so 
                        // the case of removing something before SelectedIndex is taken care of.
                        SelectionChange.End();
                    }
                    break; 
                }
 
                case NotifyCollectionChangedAction.Move: 
                {
                    SelectionChange.Validate(); 
                    break;
                }

                case NotifyCollectionChangedAction.Reset: 
                {
                    // catastrophic update -- need to resynchronize everything. 
 
                    // If we remove all the items we clear the defered selection
                    if (Items.IsEmpty) 
                        SelectionChange.CleanupDeferSelection();

                    // This is to support the MasterDetail scenario.
                    // When the Items is refreshed, Items.Current could be the old selection for this view. 
                    if (Items.CurrentItem != null && IsSynchronizedWithCurrentItemPrivate == true)
                    { 
                        // 

                        SetSelectedToCurrent(); 
                    }
                    else
                    {
                        SelectionChange.Begin(); 
                        try
                        { 
                            // Throw away all the things we don't have anymore 
                            // remove from _selectedItems anything not in Items.
                            for (int i = 0; i < _selectedItems.Count; i++) 
                            {
                                object item = _selectedItems[i];
                                if (!Items.Contains(item))          // PERF: potentially in need of optimization
                                    SelectionChange.Unselect(item); 
                            }
 
                            // Select everything in Items that is selected but isn't in the _selectedItems. 
                            if (ItemsSource == null)
                            { 
                                for (int i = 0; i < Items.Count; i++)
                                {
                                    object item = Items[i];
 
                                    // This only works for items that know they're selected:
                                    // items that are UI elements or items that have had their UI generated. 
                                    if (IndexGetIsSelected(i, item)) 
                                    {
                                        if (!_selectedItems.Contains(item)) 
                                        {
                                            SelectionChange.Select(item, true /* assumeInItemsCollection */);
                                        }
                                    } 
                                }
                            } 
                        } 
                        finally
                        { 
                            SelectionChange.End();
                        }
                    }
                    break; 
                }
                default: 
                    throw new NotSupportedException(SR.Get(SRID.UnexpectedCollectionChangeAction, e.Action)); 
            }
        } 

        /// <summary>
        /// A virtual function that is called when the selection is changed. Default behavior
        /// is to raise a SelectionChangedEvent 
        /// </summary>
        /// <param name="e">The inputs for this event. Can be raised (default behavior) or processed 
        ///   in some other way.</param> 
        protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
        { 
            RaiseEvent(e);
        }

        /// <summary> 
        ///     An event reporting that the IsKeyboardFocusWithin property changed.
        /// </summary> 
        protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e) 
        {
            base.OnIsKeyboardFocusWithinChanged(e); 

            // When focus within changes we need to update the value of IsSelectionActive property
            // In case focus is within the selector then IsSelectionActive is true
            // In case focus is within the current visual root and one the Menu/Toolbar (or any element that does not track focus) 
            // then IsSelectionActive is true
            // In all other cases IsSelectionActive is false 
            bool isSelectionActive = false; 
            if ((bool)e.NewValue)
            { 
                isSelectionActive = true;
            }
            else
            { 
                DependencyObject currentFocus = Keyboard.FocusedElement as DependencyObject;
                if (currentFocus != null) 
                { 
                    UIElement root = KeyboardNavigation.GetVisualRoot(this) as UIElement;
                    if (root != null && root.IsKeyboardFocusWithin) 
                    {
                        if (FocusManager.GetFocusScope(currentFocus) != root)
                        {
                            isSelectionActive = true; 
                        }
                    } 
                } 
            }
 
            if (isSelectionActive)
            {
                SetValue(IsSelectionActivePropertyKey, BooleanBoxes.TrueBox);
            } 
            else
            { 
                SetValue(IsSelectionActivePropertyKey, BooleanBoxes.FalseBox); 
            }
        } 

        private void OnFocusEnterMainFocusScope(object sender, EventArgs e)
        {
            // When KeyboardFocus comes back to the main focus scope and the Selector does not have focus within - clear IsSelectionActivePrivateProperty 
            if (!IsKeyboardFocusWithin)
            { 
                ClearValue(IsSelectionActivePropertyKey); 
            }
        } 

        /// <summary>
        /// Called when the value of ItemsSource changes.
        /// </summary> 
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        { 
            SetSynchronizationWithCurrentItem(); 
        }
 
        /// <summary>
        /// Prepare the element to display the item.  This may involve
        /// applying styles, setting bindings, etc.
        /// </summary> 
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        { 
            base.PrepareContainerForItemOverride(element, item); 

            // In some cases, the current TabOnceActiveElement will be pointing to an orphaned container. 
            // This causes problems with restoring focus, so to work around this we'll reset it whenever
            // the selected item is prepared.
            if (item == SelectedItem)
            { 
                KeyboardNavigation.Current.UpdateActiveElement(this, element);
            } 
        } 

        // when initialization is complete (so that all properties from markup have 
        // been set), act on IsSynchronized
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e); 
            SetSynchronizationWithCurrentItem();
        } 
 

        #endregion 

        //--------------------------------------------------------------------
        //
        //  Implementation 
        //
        //------------------------------------------------------------------- 
 
        #region Implementation
 
        // used to retrieve the value of an item, according to the SelectedValuePath
        private static readonly BindingExpressionUncommonField ItemValueBindingExpression = new BindingExpressionUncommonField();

        // True if we're really synchronizing selection and current item 
        private bool IsSynchronizedWithCurrentItemPrivate
        { 
            get { return _cacheValid[(int)CacheBits.IsSynchronizedWithCurrentItem]; } 
            set { _cacheValid[(int)CacheBits.IsSynchronizedWithCurrentItem] = value; }
        } 

        private bool SkipCoerceSelectedItemCheck
        {
            get { return _cacheValid[(int)CacheBits.SkipCoerceSelectedItemCheck]; } 
            set { _cacheValid[(int)CacheBits.SkipCoerceSelectedItemCheck] = value; }
        } 
 

        #endregion 

        #region Private Methods

        /// <summary> 
        /// Adds/Removes the given item to the collection.  Assumes the item is in the collection.
        /// </summary> 
        private void SetSelectedHelper(object item, FrameworkElement UI, bool selected) 
        {
            Debug.Assert(!SelectionChange.IsActive, "SelectionChange is already active -- use SelectionChange.Select or Unselect"); 

            bool selectable;

            selectable = ItemGetIsSelectable(item); 

            if (selectable == false && selected) 
            { 
                throw new InvalidOperationException(SR.Get(SRID.CannotSelectNotSelectableItem));
            } 

            SelectionChange.Begin();
            try
            { 
                if (selected)
                { 
                    SelectionChange.Select(item, true /* assumeInItemsCollection */); 
                }
                else 
                {
                    SelectionChange.Unselect(item);
                }
            } 
            finally
            { 
                SelectionChange.End(); 
            }
        } 

        private void OnCurrentChanged(object sender, EventArgs e)
        {
            // 
            if (IsSynchronizedWithCurrentItemPrivate)
                SetSelectedToCurrent(); 
        } 

        private void OnGeneratorStatusChanged(object sender, EventArgs e) 
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                if (HasItems) 
                {
                    Debug.Assert(!((SelectedIndex >= 0) && (_selectedItems.Count == 0)), "SelectedIndex >= 0 implies _selectedItems nonempty"); 
 
                    SelectionChange.Begin();
                    try 
                    {
                        // Things could have been added to _selectedItems before the containers were generated, so now push
                        // the IsSelected state down onto those items.
                        for (int i = 0; i < _selectedItems.Count; i++) 
                        {
                            // This could send messages back from the children, but we will ignore them b/c the selectionchange is active. 
                            ItemSetIsSelected(_selectedItems[i], true); 
                        }
                    } 
                    finally
                    {
                        SelectionChange.Cancel();
                    } 
                }
            } 
        } 

        private void SetSelectedToCurrent() 
        {
            Debug.Assert(IsSynchronizedWithCurrentItemPrivate);
            if (!_cacheValid[(int)CacheBits.SyncingSelectionAndCurrency])
            { 
                _cacheValid[(int)CacheBits.SyncingSelectionAndCurrency] = true;
 
                try 
                {
                    object item = Items.CurrentItem; 

                    if (item != null && ItemGetIsSelectable(item))
                    {
                        SelectionChange.SelectJustThisItem(item, true /* assumeInItemsCollection */); 
                    }
                    else 
                    { 
                        // Select nothing if Currency is not set.
                        SelectionChange.SelectJustThisItem(null, false); 
                    }
                }
                finally
                { 
                    _cacheValid[(int)CacheBits.SyncingSelectionAndCurrency] = false;
                } 
            } 
        }
 
        private void SetCurrentToSelected()
        {
            Debug.Assert(IsSynchronizedWithCurrentItemPrivate);
            if (!_cacheValid[(int)CacheBits.SyncingSelectionAndCurrency]) 
            {
                _cacheValid[(int)CacheBits.SyncingSelectionAndCurrency] = true; 
 
                try
                { 
                    if (_selectedItems.Count == 0)
                    {
                        // this avoid treating null as an item
                        Items.MoveCurrentToPosition(-1); 
                    }
                    else 
                    { 
                        Items.MoveCurrentTo(InternalSelectedItem);
                    } 
                }
                finally
                {
                    _cacheValid[(int)CacheBits.SyncingSelectionAndCurrency] = false; 
                }
            } 
        } 

 
        private void UpdateSelectedItems()
        {
            // Update SelectedItems.  We don't want to invalidate the property
            // because that defeats the ability of bindings to be able to listen 
            // for collection changes on that collection.  Instead we just want
            // to add all the items which are not already in the collection. 
 
            // Note: This is currently only called from SelectionChanger where SC.IsActive will be true.
            // If this is ever called from another location, ensure that SC.IsActive is true. 
            Debug.Assert(SelectionChange.IsActive, "SelectionChange.IsActive should be true");

            IList userSelectedItems = SelectedItemsImpl;
            if (userSelectedItems != null) 
            {
                InternalSelectedItemsStorage userSelectedItemsTable = new InternalSelectedItemsStorage(userSelectedItems.Count); 
                userSelectedItemsTable.UsesItemHashCodes = _selectedItems.UsesItemHashCodes; 

                for (int i = 0; i < userSelectedItems.Count; i++) 
                {
                    object userSelectedItem = userSelectedItems[i];
                    if (_selectedItems.Contains(userSelectedItem) && !userSelectedItemsTable.Contains(userSelectedItem))
                    { 
                        // cache the user's selected items into a table with O(1) lookup.
                        userSelectedItemsTable.Add(userSelectedItem); 
                    } 
                    else
                    { 
                        // Remove each thing that is in SelectedItems that's not in _selectedItems.
                        // Remove all duplicate items from userSelectedItems
                        userSelectedItems.RemoveAt(i);
                        i--; 
                    }
                } 
 
                // Add to SelectedItems everything missing that's in _selectedItems.
                foreach (object selectedItem in _selectedItems) 
                {
                    if (!userSelectedItemsTable.Contains(selectedItem))
                    {
                        userSelectedItems.Add(selectedItem); 
                    }
                } 
            } 
        }
 
        // called by SelectionChanger
        internal void UpdatePublicSelectionProperties()
        {
            EffectiveValueEntry entry = GetValueEntry( 
                        LookupEntry(SelectedIndexProperty.GlobalIndex),
                        SelectedIndexProperty, 
                        null, 
                        RequestFlags.DeferredReferences);
 
            if (!entry.IsDeferredReference)
            {
                // these are important checks to make before calling SetValue -- they
                // ensure that we are not going to clobber a coerced value 
                int selectedIndex = (int)entry.Value;
                if ((selectedIndex > Items.Count - 1) 
                    || (selectedIndex == -1 && _selectedItems.Count > 0) 
                    || (selectedIndex > -1
                        && (_selectedItems.Count == 0 || Items[selectedIndex] != _selectedItems[0]))) 
                {
                    // Use a DeferredSelectedIndexReference instead of calculating the new
                    // value now for better performance.  Most of the time no
                    // one cares what the new is, and calculating InternalSelectedIndex 
                    // be expensive because of the Items.IndexOf call
                    SetCurrentDeferredValue(SelectedIndexProperty, new DeferredSelectedIndexReference(this)); 
                } 
            }
 
            if (SelectedItem != InternalSelectedItem)
            {
                try
                { 
                    // We know that InternalSelectedItem is a correct value for SelectedItemProperty and
                    // should skip the coerce callback because it is expensive to call IndexOf and Contains 
                    SkipCoerceSelectedItemCheck = true; 
                    SetCurrentValueInternal(SelectedItemProperty, InternalSelectedItem);
                } 
                finally
                {
                    SkipCoerceSelectedItemCheck = false;
                } 

                if (_selectedItems.Count > 0) 
                { 
                    // an item has been selected, so turn off the delayed
                    // selection by SelectedValue (bug 452619) 
                    _cacheValid[(int)CacheBits.SelectedValueWaitsForItems] = false;
                }
            }
 
            if (!_cacheValid[(int)CacheBits.SelectedValueDrivesSelection])
            { 
                object desiredSelectedValue = InternalSelectedValue; 
                if (desiredSelectedValue == DependencyProperty.UnsetValue)
                { 
                    desiredSelectedValue = null;
                }

                if (!Object.Equals(SelectedValue, desiredSelectedValue)) 
                {
                    SetCurrentValueInternal(SelectedValueProperty, desiredSelectedValue); 
                } 
            }
 
            UpdateSelectedItems();
        }

        /// <summary> 
        /// Raise the SelectionChanged event.
        /// </summary> 
        private void InvokeSelectionChanged(List<object> unselectedItems, List<object> selectedItems) 
        {
            SelectionChangedEventArgs selectionChanged = new SelectionChangedEventArgs(unselectedItems, selectedItems); 

            selectionChanged.Source=this;

            OnSelectionChanged(selectionChanged); 
        }
 
        /// <summary> 
        /// Returns true if FrameworkElement representing this item has Selector.IsSelectedProperty set to true.
        /// </summary> 
        /// <param name="item"></param>
        /// <returns></returns>
        private bool ItemGetIsSelected(object item)
        { 
            if (item == null) return false;
 
            return ContainerGetIsSelected(ItemContainerGenerator.ContainerFromItem(item), item); 
        }
 
        /// <summary>
        /// Returns true if FrameworkElement representing the item at the given index
        /// has Selector.IsSelectedProperty set to true.
        /// </summary> 
        /// <param name="index"></param>
        /// <param name="item"></param> 
        /// <returns></returns> 
        private bool IndexGetIsSelected(int index, object item)
        { 
            return ContainerGetIsSelected(ItemContainerGenerator.ContainerFromIndex(index), item);
        }

        /// <summary> 
        /// Returns true if FrameworkElement (container) representing the item
        /// has Selector.IsSelectedProperty set to true. 
        /// </summary> 
        /// <param name="container"></param>
        /// <param name="item"></param> 
        /// <returns></returns>
        private bool ContainerGetIsSelected(DependencyObject container, object item)
        {
            if (container != null) 
            {
                return (bool)container.GetValue(Selector.IsSelectedProperty); 
            } 

            // In the case where the elements added *are* the containers, read it off the item could work too 
            //
            if (IsItemItsOwnContainerOverride(item))
            {
                DependencyObject element = item as DependencyObject; 

                if (element != null) 
                { 
                    return (bool)element.GetValue(Selector.IsSelectedProperty);
                } 
            }

            return false;
        } 

        /// <summary> 
        /// Set an Item to be selected.  Sets Selector.IsSelectedProperty on the FrameworkElement representing the item. 
        /// </summary>
        /// <param name="item"></param> 
        /// <param name="value"></param>
        private void ItemSetIsSelected(object item, bool value)
        {
            if (item == null) return; 

            DependencyObject container = ItemContainerGenerator.ContainerFromItem(item); 
 
            if (container != null)
            { 
                // First check that the value is different and then set it.
                if (GetIsSelected(container) != value)
                {
                    container.SetCurrentValueInternal(Selector.IsSelectedProperty, BooleanBoxes.Box(value)); 
                }
            } 
            else 
            {
                // In the case where the elements added *are* the containers, set it on the item instead of doing nothing 
                //
                if (IsItemItsOwnContainerOverride(item))
                {
                    DependencyObject element = item as DependencyObject; 

                    if (element != null) 
                    { 
                        if (GetIsSelected(element) != value)
                        { 
                            element.SetCurrentValueInternal(Selector.IsSelectedProperty, BooleanBoxes.Box(value));
                        }
                    }
                } 
            }
        } 
 
        /// <summary>
        /// Returns false if FrameworkElement representing this item has Selector.SelectableProperty set to false.  True otherwise. 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        internal static bool ItemGetIsSelectable(object item) 
        {
            if (item != null) 
            { 
                return !(item is Separator);
            } 

            return false;
        }
 
        internal static bool UiGetIsSelectable(DependencyObject o)
        { 
            if (o != null) 
            {
                if (!ItemGetIsSelectable(o)) 
                {
                    return false;
                }
                else 
                {
                    // Check the data item 
                    ItemsControl itemsControl = ItemsControl.ItemsControlFromItemContainer(o); 
                    if (itemsControl != null)
                    { 
                        object data = itemsControl.ItemContainerGenerator.ItemFromContainer(o);
                        if (data != o)
                        {
                            return ItemGetIsSelectable(data); 
                        }
                        else 
                        { 
                            return true;
                        } 
                    }
                }
            }
 
            return false;
        } 
 
        private static void OnSelected(object sender, RoutedEventArgs e)
        { 
            ((Selector)sender).NotifyIsSelectedChanged(e.OriginalSource as FrameworkElement, true, e);
        }

        private static void OnUnselected(object sender, RoutedEventArgs e) 
        {
            ((Selector)sender).NotifyIsSelectedChanged(e.OriginalSource as FrameworkElement, false, e); 
        } 

        /// <summary> 
        /// Called by handlers of Selected/Unselected or CheckedChanged events to indicate that the selection state
        /// on the item has changed and selector needs to update accordingly.
        /// </summary>
        /// <param name="container"></param> 
        /// <param name="selected"></param>
        /// <param name="e"></param> 
        /// <returns></returns> 
        internal void NotifyIsSelectedChanged(FrameworkElement container, bool selected, RoutedEventArgs e)
        { 
            // The selectionchanged event will fire at the end of the selection change.
            // We are here because this change was requested within the SelectionChange.
            // If there isn't a selection change going on now, we should do a SelectionChange.
            if (!SelectionChange.IsActive) 
            {
                if (container != null) 
                { 
                    object item = GetItemOrContainerFromContainer(container);
                    if (item != DependencyProperty.UnsetValue) 
                    {
                        SetSelectedHelper(item, container, selected);
                        e.Handled = true;
                    } 
                }
            } 
            else 
            {
                // We cause this property to change, so mark it as handled 
                e.Handled = true;
            }
        }
 
        /// <summary>
        /// Allows batch processing of selection changes so that only one SelectionChanged event is fired and 
        /// SelectedIndex is changed only once (if necessary). 
        /// </summary>
        internal SelectionChanger SelectionChange 
        {
            get
            {
                if (_selectionChangeInstance == null) 
                {
                    _selectionChangeInstance = new SelectionChanger(this); 
                } 

                return _selectionChangeInstance; 
            }
        }

        // use the first item to decide whether items support hashing correctly. 
        // Reset the algorithm used by _selectedItems accordingly.
        void ResetSelectedItemsAlgorithm() 
        { 
            if (!Items.IsEmpty)
            { 
                _selectedItems.UsesItemHashCodes = Items.CollectionView.HasReliableHashCodes();
            }
        }
 
        #region Private Properties
 
// 

/* 
        // Journaling the selection state is more complex than just a property.
        // For one thing, the selection properties may never be referenced, and
        // thus they might not have a value in the local store.  Second, one
        // property might not be sufficient (say, SelectedIndex) and another might 
        // fail serialization (i.e. SelectedItems).  With a DP that has a
        // ReadLocalValueOverride, it will be enumerated by the LocalValueEnumerator 
        // and the value can have custom serialization logic. 

        private static readonly DependencyProperty PrivateJournaledSelectionProperty = 
            DependencyProperty.Register("PrivateJournaledSelection", typeof(object), typeof(Selector),
                                        PrivateJournaledSelectionPropertyMetadata);

        private static FrameworkPropertyMetadata PrivateJournaledSelectionPropertyMetadata 
        {
            get 
            { 
                FrameworkPropertyMetadata fpm = new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Journal);
 
                fpm.ReadLocalValueOverride = new ReadLocalValueOverride(ReadPrivateJournaledSelection);
                fpm.WriteLocalValueOverride = new WriteLocalValueOverride(WritePrivateJournaledSelection);

                return fpm; 
            }
        } 
 
        private static object ReadPrivateJournaledSelection(DependencyObject d)
        { 
            // For now, just do what we were doing before -- journal SelectedIndex
            return ((Selector)d).InternalSelectedIndex;
        }
 
        private static void WritePrivateJournaledSelection(DependencyObject d, object value)
        { 
            Selector s = (Selector)d; 
            // Issue: This could throw an exception if things aren't set up in time.
            s.SelectedIndex = (int)value; 
        }
*/
        #endregion
 
        //-------------------------------------------------------------------
        // 
        //  Data 
        //
        //------------------------------------------------------------------- 

        #region Private Members

        // The selected items that we interact with.  Most of the time when SelectedItems 
        // is in use, this is identical to the value of the SelectedItems property, but
        // differs in type, and will differ in content in the case where you set or modify 
        // SelectedItems and we need to switch our selection to what was just provided. 
        // This is our internal representation of the selection and generally should be modified
        // only by SelectionChanger.  Internal classes may read this for efficiency's sake 
        // to avoid putting SelectedItems "in use" but we can't really expose this externally.
        internal InternalSelectedItemsStorage _selectedItems = new InternalSelectedItemsStorage(1);

        // Gets the selected item but doesn't use SelectedItem (avoids putting it "in use") 
        internal object InternalSelectedItem
        { 
            get 
            {
                return (_selectedItems.Count == 0) ? null : _selectedItems[0]; 
            }
        }

        /// <summary> 
        /// Index of the first item in SelectedItems or (-1) if SelectedItems is empty.
        /// </summary> 
        /// <value></value> 
        internal int InternalSelectedIndex
        { 
            get
            {
                return (_selectedItems.Count == 0) ? -1 : Items.IndexOf(_selectedItems[0]);
            } 
        }
 
        private object InternalSelectedValue 
        {
            get 
            {
                object item = InternalSelectedItem;
                object selectedValue;
 
                if (item != null)
                { 
                    BindingExpression bindingExpr = PrepareItemValueBinding(item); 

                    if (String.IsNullOrEmpty(SelectedValuePath)) 
                    {
                        // when there's no SelectedValuePath, the binding's Path
                        // is either empty (CLR) or "/InnerText" (XML)
                        string path = bindingExpr.ParentBinding.Path.Path; 
                        Debug.Assert(String.IsNullOrEmpty(path) || path == "/InnerText");
 
                        if (string.IsNullOrEmpty(path)) 
                        {
                            selectedValue = item;   // CLR - the item is its own selected value 
                        }
                        else
                        {
                            selectedValue = GetInnerText(item); // XML - use the InnerText as the selected value 
                        }
                    } 
                    else 
                    {
                        // apply the SelectedValuePath to the item 
                        bindingExpr.Activate(item);
                        selectedValue = bindingExpr.Value;
                        bindingExpr.Deactivate();
                    } 
                }
                else 
                { 
                    // no selected item - use UnsetValue (to distinguish from null, a legitimate value for the SVP)
                    selectedValue = DependencyProperty.UnsetValue; 
                }

                return selectedValue;
            } 
        }
 
        // separate method to avoid loading System.Xml until necessary 
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private object GetInnerText(object item) 
        {
            XmlNode node = item as XmlNode;

            if (node != null) 
            {
                return node.InnerText; 
            } 
            else
            { 
                return null;
            }
        }
 
        // Used by ListBox and ComboBox to determine if the mouse actually entered the
        // List/ComboBoxItem before it focus which calls BringIntoView 
        private Point _lastMousePosition = new Point(); 

        // see comment on SelectionChange property 
        private SelectionChanger _selectionChangeInstance;

        // Condense boolean bits.  Constructor takes the default value, and will resize to access up to 32 bits.
        private BitVector32 _cacheValid = new BitVector32((int)CacheBits.CanSelectMultiple); 

        [Flags] 
        private enum CacheBits 
        {
            // This flag is true while syncing the selection and the currency.  It 
            // is used to avoid reentrancy:  e.g. when the currency changes we want
            // to change the selection accordingly, but that selection change should
            // not try to change currency.
            SyncingSelectionAndCurrency    = 0x00000001, 
            CanSelectMultiple              = 0x00000002,
            IsSynchronizedWithCurrentItem  = 0x00000004, 
            SkipCoerceSelectedItemCheck    = 0x00000008, 
            SelectedValueDrivesSelection   = 0x00000010,
            SelectedValueWaitsForItems     = 0x00000020, 
        }

        private EventHandler _focusEnterMainFocusScopeEventHandler;
 
        #endregion
 
        #region Helper Classes 

        #region SelectionChanger 

        /// <summary>
        /// Helper class for selection change batching.
        /// </summary> 
        internal class SelectionChanger
        { 
            /// <summary> 
            /// Create a new SelectionChangeHelper -- there should only be one instance per Selector.
            /// </summary> 
            /// <param name="s"></param>
            internal SelectionChanger(Selector s)
            {
                _owner = s; 
                _active = false;
                _toSelect = new InternalSelectedItemsStorage(1); 
                _toUnselect = new InternalSelectedItemsStorage(1); 
                _toDeferSelect = new InternalSelectedItemsStorage(1);
            } 

            /// <summary>
            /// True if there is a SelectionChange currently in progress.
            /// </summary> 
            internal bool IsActive
            { 
                get { return _active; } 
            }
 
            /// <summary>
            /// Begin tracking selection changes.
            /// </summary>
            internal void Begin() 
            {
                Debug.Assert(_owner.CheckAccess()); 
                Debug.Assert(!_active, SR.Get(SRID.SelectionChangeActive)); 

                _active = true; 
                _toSelect.Clear();
                _toUnselect.Clear();
            }
 
            /// <summary>
            /// Commit selection changes. 
            /// </summary> 
            internal void End()
            { 
                Debug.Assert(_owner.CheckAccess());
                Debug.Assert(_active, "There must be a selection change active when you call SelectionChange.End()");

                List<object> unselected = new List<object>(); 
                List<object> selected = new List<object>();
 
                // We might have been asked to make changes that will put us in an invalid state.  Correct for this. 
                try
                { 
                    ApplyCanSelectMultiple();

                    CreateDeltaSelectionChange(unselected, selected);
 
                    _owner.UpdatePublicSelectionProperties();
                } 
                finally 
                {
                    // End the selection change -- IsActive will be false after this 
                    Cleanup();
                }

                // only raise the event if there were actually any changes applied 
                if (unselected.Count > 0 || selected.Count > 0)
                { 
                    // see bug 1459509: update Current AFTER selection change and before raising event 
                    if (_owner.IsSynchronizedWithCurrentItemPrivate)
                        _owner.SetCurrentToSelected(); 
                    _owner.InvokeSelectionChanged(unselected, selected);
                }
            }
 
            private void ApplyCanSelectMultiple()
            { 
                if (!_owner.CanSelectMultiple) 
                {
                    Debug.Assert(_toSelect.Count <= 1, "_toSelect.Count was > 1"); 

                    if (_toSelect.Count == 1) // this is all that should be selected, unselect _selectedItems
                    {
                        _toUnselect = new InternalSelectedItemsStorage(_owner._selectedItems); 
                    }
                    else // _toSelect.Count == 0, and unselect all but one of _selectedItems 
                    { 
                        // This is when CanSelectMultiple changes from true to false.
                        if (_owner._selectedItems.Count > 1 && _owner._selectedItems.Count != _toUnselect.Count + 1) 
                        {
                            // they didn't deselect enough; force deselection
                            object selectedItem = _owner._selectedItems[0];
 
                            _toUnselect.Clear();
                            foreach (object o in _owner._selectedItems) 
                            { 
                                if (o != selectedItem)
                                { 
                                    _toUnselect.Add(o);
                                }
                            }
                        } 
                    }
                } 
            } 

            private void CreateDeltaSelectionChange(List<object> unselectedItems, List<object> selectedItems) 
            {
                for (int i = 0; i < _toDeferSelect.Count; i++)
                {
                    object o = _toDeferSelect[i]; 
                    // If defered selected item exis in Items - move it to _toSelect
                    if (_owner.Items.Contains(o)) 
                    { 
                        _toSelect.Add(o);
                        _toDeferSelect.Remove(o); 
                        i--;
                    }
                }
 
                if (_toUnselect.Count > 0 || _toSelect.Count > 0)
                { 
                    if (_owner._selectedItems.UsesItemHashCodes) 
                    {
                        // Step 1: Keep current selection temp storage 
                        InternalSelectedItemsStorage currentSelectedItems = new InternalSelectedItemsStorage(_owner._selectedItems);

                        // Step 2: Clear the selection storage
                        _owner._selectedItems.Clear(); 

                        // STep 3: Process the items that need to be unselected 
                        foreach (object o in _toUnselect) 
                        {
                            _owner.ItemSetIsSelected(o, false); 
                            if (currentSelectedItems.Contains(o))
                            {
                                unselectedItems.Add(o);
                            } 
                        }
 
                        // Step 4: Add back items from the temp storage that are not in _toUnselect 
                        foreach (object o in currentSelectedItems)
                        { 
                            if (!_toUnselect.Contains(o))
                            {
                                _owner._selectedItems.Add(o);
                            } 
                        }
 
                    } 
                    else
                    { 
                        foreach (object o in _toUnselect)
                        {
                            _owner.ItemSetIsSelected(o, false);
                            if (_owner._selectedItems.Contains(o)) 
                            {
                                _owner._selectedItems.Remove(o); 
                                unselectedItems.Add(o); 
                            }
                        } 
                    }

                    foreach (object o in _toSelect)
                    { 
                        _owner.ItemSetIsSelected(o, true);
                        if (!_owner._selectedItems.Contains(o)) 
                        { 
                            _owner._selectedItems.Add(o);
                            selectedItems.Add(o); 
                        }
                    }
                }
            } 

#if never 
            private void SynchronizeSelectedIndexToSelectedItem() 
            {
                if (_owner._selectedItems.Count == 0) 
                {
                    _owner.SelectedIndex = -1;
                }
                else 
                {
                    object selectedItem = _owner.SelectedItem; 
                    object firstSelection = _owner._selectedItems[0]; 

                    // This check is only just to slightly improve perf by checking if it's in selected items before doing a reverse lookup 
                    if (selectedItem == null || firstSelection != selectedItem)
                    {
                        _owner.SelectedIndex = _owner.Items.IndexOf(firstSelection);
                    } 
                }
            } 
#endif 

            /// <summary> 
            /// Queue something to be added to the selection.  Does nothing if the item is already selected.
            /// </summary>
            /// <param name="o"></param>
            /// <param name="assumeInItemsCollection"></param> 
            /// <returns>true if the Selection was queued</returns>
            internal bool Select(object o, bool assumeInItemsCollection) 
            { 
                Debug.Assert(_owner.CheckAccess());
                Debug.Assert(_active, SR.Get(SRID.SelectionChangeNotActive)); 
                Debug.Assert(o != null, "parameter o should not be null");

                // Disallow selecting !IsSelectable things
                if (!ItemGetIsSelectable(o)) return false; 

                // Disallow selecting things not in Items.FlatView 
                if (!assumeInItemsCollection) 
                {
                    if (!_owner.Items.Contains(o)) 
                    {
                        // If user selected item is not in the Items yet - defer the selection
                        if (!_toDeferSelect.Contains(o))
                            _toDeferSelect.Add(o); 
                        return false;
                    } 
                } 

                // To support Unselect(o) / Select(o) where o is already selected. 
                if (_toUnselect.Remove(o))
                {
                    return true;
                } 

                // Ignore if the item is already selected 
                if (_owner._selectedItems.Contains(o)) return false; 

                // Ignore if the item has already been requested to be selected. 
                if (_toSelect.Contains(o)) return false;

                // enforce that we only select one thing in the CanSelectMultiple=false case.
                if (!_owner.CanSelectMultiple && _toSelect.Count > 0) 
                {
                    // If it was the item telling us this, turn around and set IsSelected = false 
                    // This will basically only happen in a Refresh situation where multiple items in the collection were selected but 
                    // CanSelectMultiple = false.
                    foreach (object item in _toSelect) 
                    {
                        _owner.ItemSetIsSelected(item, false);
                    }
                    _toSelect.Clear(); 
                }
 
                _toSelect.Add(o); 
                return true;
            } 

            /// <summary>
            /// Queue something to be removed from the selection.  Does nothing if the item is not already selected.
            /// </summary> 
            /// <param name="o"></param>
            /// <returns>true if the item was queued for unselection.</returns> 
            internal bool Unselect(object o) 
            {
 
                Debug.Assert(_owner.CheckAccess());


                Debug.Assert(_active, SR.Get(SRID.SelectionChangeNotActive)); 

                Debug.Assert(o != null, "parameter o should not be null"); 
 
                _toDeferSelect.Remove(o);
 
                // To support Select(o) / Unselect(o) where o is not already selected.
                if (_toSelect.Remove(o))
                {
                    return true; 
                }
 
                // Ignore if the item is not already selected 
                if (!_owner._selectedItems.Contains(o)) return false;
 
                // Ignore if the item has already been queued for unselection.
                if (_toUnselect.Contains(o)) return false;

                _toUnselect.Add(o); 
                return true;
            } 
 
            /// <summary>
            /// Makes sure that the current selection is valid; Performs a SelectionChange it if it's not. 
            /// </summary>
            internal void Validate()
            {
                Begin(); 
                End();
            } 
 
            /// <summary>
            /// Cancels the currently active SelectionChange. 
            /// </summary>
            internal void Cancel()
            {
 
                Debug.Assert(_owner.CheckAccess());
 
                Cleanup(); 
            }
 
            internal void CleanupDeferSelection()
            {
                if (_toDeferSelect.Count > 0)
                { 
                    _toDeferSelect.Clear();
                } 
            } 

            internal void Cleanup() 
            {
                _active = false;
                if (_toSelect.Count > 0)
                { 
                    _toSelect.Clear();
                } 
                if (_toUnselect.Count > 0) 
                {
                    _toUnselect.Clear(); 
                }
            }

            /// <summary> 
            /// Select just this item; all other items in SelectedItems will be removed.
            /// </summary> 
            /// <param name="item"></param> 
            /// <param name="assumeInItemsCollection"></param>
            internal void SelectJustThisItem(object item, bool assumeInItemsCollection) 
            {
                Begin();
                CleanupDeferSelection();
 
                try
                { 
                    // was this item already in the selection? 
                    bool isSelected = false;
 
                    // go backwards in case a selection is rejected; then they'll still have the same SelectedItem
                    for (int i = _owner._selectedItems.Count - 1; i >= 0; i--)
                    {
                        if (item != _owner._selectedItems[i]) 
                        {
                            Unselect(_owner._selectedItems[i]); 
                        } 
                        else
                        { 
                            isSelected = true;
                        }
                    }
 
                    if (!isSelected && item != null && item != DependencyProperty.UnsetValue)
                    { 
                        Select(item, assumeInItemsCollection); 
                    }
                } 
                finally
                {
                    End();
                } 
            }
 
            private Selector _owner; 
            private InternalSelectedItemsStorage _toSelect;
            private InternalSelectedItemsStorage _toUnselect; 
            private InternalSelectedItemsStorage _toDeferSelect; // Keep the items that cannot be selected because they are not in _owner.Items
            private bool _active;
        }
 
        #endregion
 
        #region InternalSelectedItemsStorage 

        internal class InternalSelectedItemsStorage : IEnumerable 
        {
            internal InternalSelectedItemsStorage(int capacity)
            {
                _list = new ArrayList(capacity); 
                _set = new Dictionary<object, object>(capacity);
            } 
 
            internal InternalSelectedItemsStorage(InternalSelectedItemsStorage collection)
            { 
                _list = new ArrayList(collection._list);

                if (collection.UsesItemHashCodes)
                { 
                    _set = new Dictionary<object, object>(collection._set);
                } 
            } 

            public void Add(object t) 
            {
                if (_set != null)
                {
                    _set.Add(t, null); 
                }
                _list.Add(t); 
            } 

            public bool Remove(object t) 
            {
                if (_set != null)
                {
                    if (_set.Remove(t)) 
                    {
                        _list.Remove(t); 
 
                        return true;
                    } 
                }
                else
                {
                    int index = _list.IndexOf(t); 
                    if (index >= 0)
                    { 
                        _list.RemoveAt(index); 
                        return true;
                    } 
                }

                return false;
            } 

            public bool Contains(object t) 
            { 
                if (_set != null)
                { 
                    return _set.ContainsKey(t);
                }
                else
                { 
                    return _list.Contains(t);
                } 
            } 

            public object this[int index] 
            {
                get
                {
                    return _list[index]; 
                }
            } 
 
            public void Clear()
            { 
                _list.Clear();
                if (_set != null)
                {
                    _set.Clear(); 
                }
            } 
 
            public int Count
            { 
                get
                {
                    return _list.Count;
                } 
            }
 
            IEnumerator IEnumerable.GetEnumerator() 
            {
                return _list.GetEnumerator(); 
            }

            // If the underlying items don't implement GetHashCode according to
            // guidelines (i.e. if an item's hashcode can change during the item's 
            // lifetime) we can't use any hash-based data structures like Dictionary,
            // Hashtable, etc.  The principal offender is DataRowView.  (bug 1583080) 
            public bool UsesItemHashCodes 
            {
                get { return _set != null; } 
                set
                {
                    if (value == true && _set == null)
                    { 
                        _set = new Dictionary<object, object>(_list.Count);
                        for (int i=0; i<_list.Count; ++i) 
                        { 
                            _set.Add(_list[i], null);
                        } 
                    }
                    else if (value == false)
                    {
                        _set = null; 
                    }
                } 
            } 

            private ArrayList _list; 
            private Dictionary<object, object> _set;

        }
 
        #endregion InternalSelectedItemsStorage
 
        #endregion 

        #endregion 
    }
}


