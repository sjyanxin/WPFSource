//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//--------------------------------------------------------------------------- 

using System; 
using System.Collections; 
using System.Collections.Generic;
using System.Collections.ObjectModel; 
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading; 
using System.Windows.Controls.Primitives;
using System.Windows.Data; 
using System.Windows; 
using System.Windows.Media;
using System.Windows.Markup; 
using System.Windows.Input;

using MS.Utility;
using MS.Internal; 
using MS.Internal.Controls;
using MS.Internal.Data; 
using MS.Internal.Hashing.PresentationFramework;    // HashHelper 
using MS.Internal.KnownBoxes;
using MS.Internal.PresentationFramework; 
using MS.Internal.Utility;

namespace System.Windows.Controls
{ 
    /// <summary>
    ///     The base class for all controls that have multiple children. 
    /// </summary> 
    /// <remarks>
    ///     ItemsControl adds Items, ItemTemplate, and Part features to a Control. 
    /// </remarks>
    //
    [DefaultEvent("OnItemsChanged"), DefaultProperty("Items")]
    [ContentProperty("Items")] 
    [StyleTypedProperty(Property = "ItemContainerStyle", StyleTargetType = typeof(FrameworkElement))]
    [Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)] // cannot be read & localized as string 
    public class ItemsControl : Control, IAddChild, IGeneratorHost 
    {
        #region Constructors 

        /// <summary>
        ///     Default ItemsControl constructor
        /// </summary> 
        /// <remarks>
        ///     Automatic determination of current Dispatcher. Use alternative constructor 
        ///     that accepts a Dispatcher for best performance. 
        /// </remarks>
        public ItemsControl() : base() 
        {
        }

        static ItemsControl() 
        {
            // 
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ItemsControl), new FrameworkPropertyMetadata(typeof(ItemsControl))); 
            _dType = DependencyObjectType.FromSystemTypeInternal(typeof(ItemsControl));
            EventManager.RegisterClassHandler(typeof(ItemsControl), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotFocus)); 
        }

        private void CreateItemCollectionAndGenerator()
        { 
            _items = new ItemCollection(this);
 
            // the generator must attach its collection change handler before 
            // the control itself, so that the generator is up-to-date by the
            // time the control tries to use it (bug 892806 et al.) 
            _itemContainerGenerator = new ItemContainerGenerator(this);

            _itemContainerGenerator.ChangeAlternationCount();
 
            ((INotifyCollectionChanged)_items).CollectionChanged += new NotifyCollectionChangedEventHandler(OnItemCollectionChanged);
 
            if (IsInitPending) 
            {
                _items.BeginInit(); 
            }
            else if (IsInitialized)
            {
                _items.BeginInit(); 
                _items.EndInit();
            } 
 
            ((INotifyCollectionChanged)_groupStyle).CollectionChanged += new NotifyCollectionChangedEventHandler(OnGroupStyleChanged);
        } 

        #endregion

        #region Properties 

        /// <summary> 
        ///     Items is the collection of data that is used to generate the content 
        ///     of this control.
        /// </summary> 
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Bindable(true), CustomCategory("Content")]
        public ItemCollection Items
        { 
            get
            { 
                if (_items == null) 
                {
                    CreateItemCollectionAndGenerator(); 
                }

                return _items;
            } 
        }
 
        /// <summary> 
        /// This method is used by TypeDescriptor to determine if this property should
        /// be serialized. 
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeItems()
        { 
            return HasItems;
        } 
 
        /// <summary>
        ///     The DependencyProperty for the ItemsSource property. 
        ///     Flags:              None
        ///     Default Value:      null
        /// </summary>
        [CommonDependencyProperty] 
        public static readonly DependencyProperty ItemsSourceProperty
            = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(ItemsControl), 
                                          new FrameworkPropertyMetadata((IEnumerable)null, 
                                                                        new PropertyChangedCallback(OnItemsSourceChanged)));
 
        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ItemsControl ic = (ItemsControl) d;
            IEnumerable oldValue = (IEnumerable)e.OldValue; 
            IEnumerable newValue = (IEnumerable)e.NewValue;
 
            ItemValueStorageField.ClearValue(d); 

            // distinguish between an explicit null value and one arising from 
            // a Binding.  The former means to return to normal mode,
            // the latter means to use ItemsSource mode, but with a
            // null collection.
            if (e.NewValue == null && !BindingOperations.IsDataBound(d, ItemsSourceProperty)) 
            {
                ic.Items.ClearItemsSource(); 
            } 
            else
            { 
                ic.Items.SetItemsSource(newValue);
            }
            ic.OnItemsSourceChanged(oldValue, newValue);
        } 

        /// <summary> 
        /// Called when the value of ItemsSource changes. 
        /// </summary>
        protected virtual void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) 
        {
        }

        /// <summary> 
        ///     ItemsSource specifies a collection used to generate the content of
        /// this control.  This provides a simple way to use exactly one collection 
        /// as the source of content for this control. 
        /// </summary>
        /// <remarks> 
        ///     Any existing contents of the Items collection is replaced when this
        /// property is set. The Items collection will be made ReadOnly and FixedSize.
        ///     When ItemsSource is in use, setting this property to null will remove
        /// the collection and restore use to Items (which will be an empty ItemCollection). 
        ///     When ItemsSource is not in use, the value of this property is null, and
        /// setting it to null has no effect. 
        /// </remarks> 
        [Bindable(true), CustomCategory("Content")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        public IEnumerable ItemsSource
        {
            get { return Items.ItemsSource; }
            set 
            {
                if (value == null) 
                { 
                    ClearValue(ItemsSourceProperty);
                } 
                else
                {
                    SetValue(ItemsSourceProperty, value);
                } 
            }
        } 
 
        /// <summary>
        /// The ItemContainerGenerator associated with this control 
        /// </summary>
        [Bindable(false), Browsable(false), EditorBrowsable(EditorBrowsableState.Advanced)]
        public ItemContainerGenerator ItemContainerGenerator
        { 
            get
            { 
                if (_itemContainerGenerator == null) 
                {
                    CreateItemCollectionAndGenerator(); 
                }

                return _itemContainerGenerator;
            } 
        }
 
        /// <summary> 
        ///     Returns enumerator to logical children
        /// </summary> 
        protected internal override IEnumerator LogicalChildren
        {
            get
            { 
                if (!HasItems)
                { 
                    return EmptyEnumerator.Instance; 
                }
 
                // Items in direct-mode of ItemCollection are the only model children.
                // note: the enumerator walks the ItemCollection.InnerList as-is,
                // no flattening of any content on model children level!
                return this.Items.LogicalChildren; 
            }
        } 
 
        private void OnItemCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        { 
            SetValue(HasItemsPropertyKey, (_items != null) && !_items.IsEmpty);

            // If the focused item is removed, drop our reference to it.
            if ((e.Action == NotifyCollectionChangedAction.Remove && _focusedItem != null && _focusedItem.Equals(e.OldItems[0])) 
                || (e.Action == NotifyCollectionChangedAction.Reset))
            { 
                _focusedItem = null; 
            }
 
            OnItemsChanged(e);
        }

        /// <summary> 
        ///     This method is invoked when the Items property changes.
        /// </summary> 
        protected virtual void OnItemsChanged(NotifyCollectionChangedEventArgs e) 
        {
        } 

        /// <summary>
        ///     The key needed set a read-only property.
        /// </summary> 
        internal static readonly DependencyPropertyKey HasItemsPropertyKey =
                DependencyProperty.RegisterReadOnly( 
                        "HasItems", 
                        typeof(bool),
                        typeof(ItemsControl), 
                        new FrameworkPropertyMetadata(BooleanBoxes.FalseBox, OnVisualStatePropertyChanged));

        /// <summary>
        ///     The DependencyProperty for the HasItems property. 
        ///     Flags:              None
        ///     Other:              Read-Only 
        ///     Default Value:      false 
        /// </summary>
        public static readonly DependencyProperty HasItemsProperty = 
                HasItemsPropertyKey.DependencyProperty;

        /// <summary>
        ///     True if Items.Count > 0, false otherwise. 
        /// </summary>
        [Bindable(false), Browsable(false)] 
        public bool HasItems 
        {
            get { return (bool) GetValue(HasItemsProperty); } 
        }

        /// <summary>
        ///     The DependencyProperty for the DisplayMemberPath property. 
        ///     Flags:              none
        ///     Default Value:      string.Empty 
        /// </summary> 
        public static readonly DependencyProperty DisplayMemberPathProperty =
                DependencyProperty.Register( 
                        "DisplayMemberPath",
                        typeof(string),
                        typeof(ItemsControl),
                        new FrameworkPropertyMetadata( 
                                string.Empty,
                                new PropertyChangedCallback(OnDisplayMemberPathChanged))); 
 
        /// <summary>
        ///     DisplayMemberPath is a simple way to define a default template 
        ///     that describes how to convert Items into UI elements by using
        ///     the specified path.
        /// </summary>
        [Bindable(true), CustomCategory("Content")] 
        public string DisplayMemberPath
        { 
            get { return (string) GetValue(DisplayMemberPathProperty); } 
            set { SetValue(DisplayMemberPathProperty, value); }
        } 

        /// <summary>
        ///     Called when DisplayMemberPathProperty is invalidated on "d."
        /// </summary> 
        private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
            ItemsControl ctrl = (ItemsControl) d; 

            ctrl.OnDisplayMemberPathChanged((string)e.OldValue, (string)e.NewValue); 
            ctrl.UpdateDisplayMemberTemplateSelector();
        }

        // DisplayMemberPath and ItemStringFormat use the ItemTemplateSelector property 
        // to achieve the desired result.  When either of these properties change,
        // update the ItemTemplateSelector property here. 
        private void UpdateDisplayMemberTemplateSelector() 
        {
            string displayMemberPath = DisplayMemberPath; 
            string itemStringFormat = ItemStringFormat;

            if (!String.IsNullOrEmpty(displayMemberPath) || !String.IsNullOrEmpty(itemStringFormat))
            { 
                // One or both of DisplayMemberPath and ItemStringFormat are desired.
                // Set ItemTemplateSelector to an appropriate object, provided that 
                // this doesn't conflict with the user's own setting. 
                DataTemplateSelector itemTemplateSelector = ItemTemplateSelector;
                if (itemTemplateSelector != null && !(itemTemplateSelector is DisplayMemberTemplateSelector)) 
                {
                    // if ITS was actually set to something besides a DisplayMember selector,
                    // it's an error to overwrite it with a DisplayMember selector
                    // unless ITS came from a style and DMP is local 
                    if (ReadLocalValue(ItemTemplateSelectorProperty) != DependencyProperty.UnsetValue ||
                        ReadLocalValue(DisplayMemberPathProperty) == DependencyProperty.UnsetValue) 
                    { 
                        throw new InvalidOperationException(SR.Get(SRID.DisplayMemberPathAndItemTemplateSelectorDefined));
                    } 
                }

                // now set the ItemTemplateSelector to use the new DisplayMemberPath and ItemStringFormat
                ItemTemplateSelector = new DisplayMemberTemplateSelector(DisplayMemberPath, ItemStringFormat); 
            }
            else 
            { 
                // Neither property is desired.  Clear the ItemTemplateSelector if
                // we had set it earlier. 
                if (ItemTemplateSelector is DisplayMemberTemplateSelector)
                {
                    ClearValue(ItemTemplateSelectorProperty);
                } 
            }
        } 
 
        /// <summary>
        ///     This method is invoked when the DisplayMemberPath property changes. 
        /// </summary>
        /// <param name="oldDisplayMemberPath">The old value of the DisplayMemberPath property.</param>
        /// <param name="newDisplayMemberPath">The new value of the DisplayMemberPath property.</param>
        protected virtual void OnDisplayMemberPathChanged(string oldDisplayMemberPath, string newDisplayMemberPath) 
        {
        } 
 
        /// <summary>
        ///     The DependencyProperty for the ItemTemplate property. 
        ///     Flags:              none
        ///     Default Value:      null
        /// </summary>
        [CommonDependencyProperty] 
        public static readonly DependencyProperty ItemTemplateProperty =
                DependencyProperty.Register( 
                        "ItemTemplate", 
                        typeof(DataTemplate),
                        typeof(ItemsControl), 
                        new FrameworkPropertyMetadata(
                                (DataTemplate) null,
                                new PropertyChangedCallback(OnItemTemplateChanged)));
 
        /// <summary>
        ///     ItemTemplate is the template used to display each item. 
        /// </summary> 
        [Bindable(true), CustomCategory("Content")]
        public DataTemplate ItemTemplate 
        {
            get { return (DataTemplate) GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        } 

        /// <summary> 
        ///     Called when ItemTemplateProperty is invalidated on "d." 
        /// </summary>
        /// <param name="d">The object on which the property was invalidated.</param> 
        /// <param name="e">EventArgs that contains the old and new values for this property</param>
        private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ItemsControl) d).OnItemTemplateChanged((DataTemplate) e.OldValue, (DataTemplate) e.NewValue); 
        }
 
        /// <summary> 
        ///     This method is invoked when the ItemTemplate property changes.
        /// </summary> 
        /// <param name="oldItemTemplate">The old value of the ItemTemplate property.</param>
        /// <param name="newItemTemplate">The new value of the ItemTemplate property.</param>
        protected virtual void OnItemTemplateChanged(DataTemplate oldItemTemplate, DataTemplate newItemTemplate)
        { 
            CheckTemplateSource();
 
            if (_itemContainerGenerator != null) 
            {
                _itemContainerGenerator.Refresh(); 
            }
        }

 
        /// <summary>
        ///     The DependencyProperty for the ItemTemplateSelector property. 
        ///     Flags:              none 
        ///     Default Value:      null
        /// </summary> 
        [CommonDependencyProperty]
        public static readonly DependencyProperty ItemTemplateSelectorProperty =
                DependencyProperty.Register(
                        "ItemTemplateSelector", 
                        typeof(DataTemplateSelector),
                        typeof(ItemsControl), 
                        new FrameworkPropertyMetadata( 
                                (DataTemplateSelector) null,
                                new PropertyChangedCallback(OnItemTemplateSelectorChanged))); 

        /// <summary>
        ///     ItemTemplateSelector allows the application writer to provide custom logic
        ///     for choosing the template used to display each item. 
        /// </summary>
        /// <remarks> 
        ///     This property is ignored if <seealso cref="ItemTemplate"/> is set. 
        /// </remarks>
        [Bindable(true), CustomCategory("Content")] 
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataTemplateSelector ItemTemplateSelector
        {
            get { return (DataTemplateSelector) GetValue(ItemTemplateSelectorProperty); } 
            set { SetValue(ItemTemplateSelectorProperty, value); }
        } 
 
        /// <summary>
        ///     Called when ItemTemplateSelectorProperty is invalidated on "d." 
        /// </summary>
        /// <param name="d">The object on which the property was invalidated.</param>
        /// <param name="e">EventArgs that contains the old and new values for this property</param>
        private static void OnItemTemplateSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        {
            ((ItemsControl)d).OnItemTemplateSelectorChanged((DataTemplateSelector) e.OldValue, (DataTemplateSelector) e.NewValue); 
        } 

        /// <summary> 
        ///     This method is invoked when the ItemTemplateSelector property changes.
        /// </summary>
        /// <param name="oldItemTemplateSelector">The old value of the ItemTemplateSelector property.</param>
        /// <param name="newItemTemplateSelector">The new value of the ItemTemplateSelector property.</param> 
        protected virtual void OnItemTemplateSelectorChanged(DataTemplateSelector oldItemTemplateSelector, DataTemplateSelector newItemTemplateSelector)
        { 
            CheckTemplateSource(); 

            if ((_itemContainerGenerator != null) && (ItemTemplate == null)) 
            {
                _itemContainerGenerator.Refresh();
            }
        } 

        /// <summary> 
        ///     The DependencyProperty for the ItemStringFormat property. 
        ///     Flags:              None
        ///     Default Value:      null 
        /// </summary>
        [CommonDependencyProperty]
        public static readonly DependencyProperty ItemStringFormatProperty =
                DependencyProperty.Register( 
                        "ItemStringFormat",
                        typeof(String), 
                        typeof(ItemsControl), 
                        new FrameworkPropertyMetadata(
                                (String) null, 
                              new PropertyChangedCallback(OnItemStringFormatChanged)));


        /// <summary> 
        ///     ItemStringFormat is the format used to display an item (or a
        ///     property of an item, as declared by DisplayMemberPath) as a string. 
        ///     This arises only when no template is available. 
        /// </summary>
        [Bindable(true), CustomCategory("Content")] 
        public String ItemStringFormat
        {
            get { return (String) GetValue(ItemStringFormatProperty); }
            set { SetValue(ItemStringFormatProperty, value); } 
        }
 
        /// <summary> 
        ///     Called when ItemStringFormatProperty is invalidated on "d."
        /// </summary> 
        private static void OnItemStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ItemsControl ctrl = (ItemsControl)d;
 
            ctrl.OnItemStringFormatChanged((String) e.OldValue, (String) e.NewValue);
            ctrl.UpdateDisplayMemberTemplateSelector(); 
        } 

        /// <summary> 
        ///     This method is invoked when the ItemStringFormat property changes.
        /// </summary>
        /// <param name="oldItemStringFormat">The old value of the ItemStringFormat property.</param>
        /// <param name="newItemStringFormat">The new value of the ItemStringFormat property.</param> 
        protected virtual void OnItemStringFormatChanged(String oldItemStringFormat, String newItemStringFormat)
        { 
        } 

 
        /// <summary>
        ///     The DependencyProperty for the ItemBindingGroup property.
        ///     Flags:              None
        ///     Default Value:      null 
        /// </summary>
        [CommonDependencyProperty] 
        public static readonly DependencyProperty ItemBindingGroupProperty = 
                DependencyProperty.Register(
                        "ItemBindingGroup", 
                        typeof(BindingGroup),
                        typeof(ItemsControl),
                        new FrameworkPropertyMetadata(
                                (BindingGroup) null, 
                              new PropertyChangedCallback(OnItemBindingGroupChanged)));
 
 
        /// <summary>
        ///     ItemBindingGroup declares a BindingGroup to be used as a "master" 
        ///     for the generated containers.  Each container's BindingGroup is set
        ///     to a copy of the master, sharing the same set of validation rules,
        ///     but managing its own collection of bindings.
        /// </summary> 
        [Bindable(true), CustomCategory("Content")]
        public BindingGroup ItemBindingGroup 
        { 
            get { return (BindingGroup) GetValue(ItemBindingGroupProperty); }
            set { SetValue(ItemBindingGroupProperty, value); } 
        }

        /// <summary>
        ///     Called when ItemBindingGroupProperty is invalidated on "d." 
        /// </summary>
        private static void OnItemBindingGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        { 
            ItemsControl ctrl = (ItemsControl)d;
 
            ctrl.OnItemBindingGroupChanged((BindingGroup) e.OldValue, (BindingGroup) e.NewValue);
        }

        /// <summary> 
        ///     This method is invoked when the ItemBindingGroup property changes.
        /// </summary> 
        /// <param name="oldItemBindingGroup">The old value of the ItemBindingGroup property.</param> 
        /// <param name="newItemBindingGroup">The new value of the ItemBindingGroup property.</param>
        protected virtual void OnItemBindingGroupChanged(BindingGroup oldItemBindingGroup, BindingGroup newItemBindingGroup) 
        {
        }

 
        /// <summary>
        /// Throw if more than one of DisplayMemberPath, xxxTemplate and xxxTemplateSelector 
        /// properties are set on the given element. 
        /// </summary>
        private void CheckTemplateSource() 
        {
            if (string.IsNullOrEmpty(DisplayMemberPath))
            {
                Helper.CheckTemplateAndTemplateSelector("Item", ItemTemplateProperty, ItemTemplateSelectorProperty, this); 
            }
            else 
            { 
                if (!(this.ItemTemplateSelector is DisplayMemberTemplateSelector))
                { 
                    throw new InvalidOperationException(SR.Get(SRID.ItemTemplateSelectorBreaksDisplayMemberPath));
                }
                if (Helper.IsTemplateDefined(ItemTemplateProperty, this))
                { 
                    throw new InvalidOperationException(SR.Get(SRID.DisplayMemberPathAndItemTemplateDefined));
                } 
            } 
        }
 
        /// <summary>
        ///     The DependencyProperty for the ItemContainerStyle property.
        ///     Flags:              none
        ///     Default Value:      null 
        /// </summary>
        [CommonDependencyProperty] 
        public static readonly DependencyProperty ItemContainerStyleProperty = 
                DependencyProperty.Register(
                        "ItemContainerStyle", 
                        typeof(Style),
                        typeof(ItemsControl),
                        new FrameworkPropertyMetadata(
                                (Style) null, 
                                new PropertyChangedCallback(OnItemContainerStyleChanged)));
 
        /// <summary> 
        ///     ItemContainerStyle is the style that is applied to the container element generated
        ///     for each item. 
        /// </summary>
        [Bindable(true), Category("Content")]
        public Style ItemContainerStyle
        { 
            get { return (Style) GetValue(ItemContainerStyleProperty); }
            set { SetValue(ItemContainerStyleProperty, value); } 
        } 

        /// <summary> 
        ///     Called when ItemContainerStyleProperty is invalidated on "d."
        /// </summary>
        /// <param name="d">The object on which the property was invalidated.</param>
        /// <param name="e">EventArgs that contains the old and new values for this property</param> 
        private static void OnItemContainerStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
            ((ItemsControl) d).OnItemContainerStyleChanged((Style) e.OldValue, (Style) e.NewValue); 
        }
 
        /// <summary>
        ///     This method is invoked when the ItemContainerStyle property changes.
        /// </summary>
        /// <param name="oldItemContainerStyle">The old value of the ItemContainerStyle property.</param> 
        /// <param name="newItemContainerStyle">The new value of the ItemContainerStyle property.</param>
        protected virtual void OnItemContainerStyleChanged(Style oldItemContainerStyle, Style newItemContainerStyle) 
        { 
            Helper.CheckStyleAndStyleSelector("ItemContainer", ItemContainerStyleProperty, ItemContainerStyleSelectorProperty, this);
 
            if (_itemContainerGenerator != null)
            {
                _itemContainerGenerator.Refresh();
            } 
        }
 
 
        /// <summary>
        ///     The DependencyProperty for the ItemContainerStyleSelector property. 
        ///     Flags:              none
        ///     Default Value:      null
        /// </summary>
        [CommonDependencyProperty] 
        public static readonly DependencyProperty ItemContainerStyleSelectorProperty =
                DependencyProperty.Register( 
                        "ItemContainerStyleSelector", 
                        typeof(StyleSelector),
                        typeof(ItemsControl), 
                        new FrameworkPropertyMetadata(
                                (StyleSelector) null,
                                new PropertyChangedCallback(OnItemContainerStyleSelectorChanged)));
 
        /// <summary>
        ///     ItemContainerStyleSelector allows the application writer to provide custom logic 
        ///     to choose the style to apply to each generated container element. 
        /// </summary>
        /// <remarks> 
        ///     This property is ignored if <seealso cref="ItemContainerStyle"/> is set.
        /// </remarks>
        [Bindable(true), Category("Content")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        public StyleSelector ItemContainerStyleSelector
        { 
            get { return (StyleSelector) GetValue(ItemContainerStyleSelectorProperty); } 
            set { SetValue(ItemContainerStyleSelectorProperty, value); }
        } 

        /// <summary>
        ///     Called when ItemContainerStyleSelectorProperty is invalidated on "d."
        /// </summary> 
        /// <param name="d">The object on which the property was invalidated.</param>
        /// <param name="e">EventArgs that contains the old and new values for this property</param> 
        private static void OnItemContainerStyleSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        {
            ((ItemsControl) d).OnItemContainerStyleSelectorChanged((StyleSelector) e.OldValue, (StyleSelector) e.NewValue); 
        }

        /// <summary>
        ///     This method is invoked when the ItemContainerStyleSelector property changes. 
        /// </summary>
        /// <param name="oldItemContainerStyleSelector">The old value of the ItemContainerStyleSelector property.</param> 
        /// <param name="newItemContainerStyleSelector">The new value of the ItemContainerStyleSelector property.</param> 
        protected virtual void OnItemContainerStyleSelectorChanged(StyleSelector oldItemContainerStyleSelector, StyleSelector newItemContainerStyleSelector)
        { 
            Helper.CheckStyleAndStyleSelector("ItemContainer", ItemContainerStyleProperty, ItemContainerStyleSelectorProperty, this);

            if ((_itemContainerGenerator != null) && (ItemContainerStyle == null))
            { 
                _itemContainerGenerator.Refresh();
            } 
        } 

        /// <summary> 
        ///     Returns the ItemsControl for which element is an ItemsHost.
        ///     More precisely, if element is marked by setting IsItemsHost="true"
        ///     in the style for an ItemsControl, or if element is a panel created
        ///     by the ItemsPresenter for an ItemsControl, return that ItemsControl. 
        ///     Otherwise, return null.
        /// </summary> 
        public static ItemsControl GetItemsOwner(DependencyObject element) 
        {
            ItemsControl container = null; 
            Panel panel = element as Panel;

            if (panel != null && panel.IsItemsHost)
            { 
                // see if element was generated for an ItemsPresenter
                ItemsPresenter ip = ItemsPresenter.FromPanel(panel); 
 
                if (ip != null)
                { 
                    // if so use the element whose style begat the ItemsPresenter
                    container = ip.Owner;
                }
                else 
                {
                    // otherwise use element's templated parent 
                    container = panel.TemplatedParent as ItemsControl; 
                }
            } 

            return container;
        }
 

        /// <summary> 
        ///     The DependencyProperty for the ItemsPanel property. 
        ///     Flags:              none
        ///     Default Value:      null 
        /// </summary>
        [CommonDependencyProperty]
        public static readonly DependencyProperty ItemsPanelProperty
            = DependencyProperty.Register("ItemsPanel", typeof(ItemsPanelTemplate), typeof(ItemsControl), 
                                          new FrameworkPropertyMetadata(GetDefaultItemsPanelTemplate(),
                                                                        new PropertyChangedCallback(OnItemsPanelChanged))); 
 
        private static ItemsPanelTemplate GetDefaultItemsPanelTemplate()
        { 
            ItemsPanelTemplate template = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
            template.Seal();
            return template;
        } 

        /// <summary> 
        ///     ItemsPanel is the panel that controls the layout of items. 
        ///     (More precisely, the panel that controls layout is created
        ///     from the template given by ItemsPanel.) 
        /// </summary>
        [Bindable(false)]
        public ItemsPanelTemplate ItemsPanel
        { 
            get { return (ItemsPanelTemplate) GetValue(ItemsPanelProperty); }
            set { SetValue(ItemsPanelProperty, value); } 
        } 

        /// <summary> 
        ///     Called when ItemsPanelProperty is invalidated on "d."
        /// </summary>
        /// <param name="d">The object on which the property was invalidated.</param>
        /// <param name="e">EventArgs that contains the old and new values for this property</param> 
        private static void OnItemsPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { 
            ((ItemsControl) d).OnItemsPanelChanged((ItemsPanelTemplate) e.OldValue, (ItemsPanelTemplate) e.NewValue); 
        }
 
        /// <summary>
        ///     This method is invoked when the ItemsPanel property changes.
        /// </summary>
        /// <param name="oldItemsPanel">The old value of the ItemsPanel property.</param> 
        /// <param name="newItemsPanel">The new value of the ItemsPanel property.</param>
        protected virtual void OnItemsPanelChanged(ItemsPanelTemplate oldItemsPanel, ItemsPanelTemplate newItemsPanel) 
        { 
            ItemContainerGenerator.OnPanelChanged();
        } 


        private static readonly DependencyPropertyKey IsGroupingPropertyKey =
            DependencyProperty.RegisterReadOnly("IsGrouping", typeof(bool), typeof(ItemsControl), new FrameworkPropertyMetadata(BooleanBoxes.FalseBox)); 

        /// <summary> 
        ///     The DependencyProperty for the IsGrouping property. 
        /// </summary>
        public static readonly DependencyProperty IsGroupingProperty = IsGroupingPropertyKey.DependencyProperty; 

        /// <summary>
        ///     Returns whether the control is using grouping.
        /// </summary> 
        [Bindable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        public bool IsGrouping 
        {
            get 
            {
                return (bool)GetValue(IsGroupingProperty);
            }
        } 

        /// <summary> 
        /// The collection of GroupStyle objects that describes the display of 
        /// each level of grouping.  The entry at index 0 describes the top level
        /// groups, the entry at index 1 describes the next level, and so forth. 
        /// If there are more levels of grouping than entries in the collection,
        /// the last entry is used for the extra levels.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] 
        public ObservableCollection<GroupStyle> GroupStyle
        { 
            get { return _groupStyle; } 
        }
 
        /// <summary>
        /// This method is used by TypeDescriptor to determine if this property should
        /// be serialized.
        /// </summary> 
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeGroupStyle() 
        { 
            return (GroupStyle.Count > 0);
        } 

        private void OnGroupStyleChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_itemContainerGenerator != null) 
            {
                _itemContainerGenerator.Refresh(); 
            } 
        }
 

        /// <summary>
        ///     The DependencyProperty for the GroupStyleSelector property.
        ///     Flags:              none 
        ///     Default Value:      null
        /// </summary> 
        public static readonly DependencyProperty GroupStyleSelectorProperty 
            = DependencyProperty.Register("GroupStyleSelector", typeof(GroupStyleSelector), typeof(ItemsControl),
                                          new FrameworkPropertyMetadata((GroupStyleSelector)null, 
                                                                        new PropertyChangedCallback(OnGroupStyleSelectorChanged)));

        /// <summary>
        ///     GroupStyleSelector allows the app writer to provide custom selection logic 
        ///     for a GroupStyle to apply to each group collection.
        /// </summary> 
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        [Bindable(true), CustomCategory("Content")]
        public GroupStyleSelector GroupStyleSelector 
        {
            get { return (GroupStyleSelector) GetValue(GroupStyleSelectorProperty); }
            set { SetValue(GroupStyleSelectorProperty, value); }
        } 

        /// <summary> 
        ///     Called when GroupStyleSelectorProperty is invalidated on "d." 
        /// </summary>
        /// <param name="d">The object on which the property was invalidated.</param> 
        /// <param name="e">EventArgs that contains the old and new values for this property</param>
        private static void OnGroupStyleSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ItemsControl) d).OnGroupStyleSelectorChanged((GroupStyleSelector) e.OldValue, (GroupStyleSelector) e.NewValue); 
        }
 
        /// <summary> 
        ///     This method is invoked when the GroupStyleSelector property changes.
        /// </summary> 
        /// <param name="oldGroupStyleSelector">The old value of the GroupStyleSelector property.</param>
        /// <param name="newGroupStyleSelector">The new value of the GroupStyleSelector property.</param>
        protected virtual void OnGroupStyleSelectorChanged(GroupStyleSelector oldGroupStyleSelector, GroupStyleSelector newGroupStyleSelector)
        { 
            if (_itemContainerGenerator != null)
            { 
                _itemContainerGenerator.Refresh(); 
            }
        } 

        /// <summary>
        ///     The DependencyProperty for the AlternationCount property.
        ///     Flags:              none 
        ///     Default Value:      0
        /// </summary> 
        public static readonly DependencyProperty AlternationCountProperty = 
                DependencyProperty.Register(
                        "AlternationCount", 
                        typeof(int),
                        typeof(ItemsControl),
                        new FrameworkPropertyMetadata(
                                (int)0, 
                                new PropertyChangedCallback(OnAlternationCountChanged)));
 
        /// <summary> 
        ///     AlternationCount controls the range of values assigned to the
        ///     AlternationIndex property attached to each generated container.  The 
        ///     default value 0 means "do not set AlternationIndex".  A positive
        ///     value means "assign AlternationIndex in the range [0, AlternationCount)
        ///     so that adjacent containers receive different values".
        /// </summary> 
        /// <remarks>
        ///     By referring to AlternationIndex in a trigger or binding (typically 
        ///     in the ItemContainerStyle), you can make the appearance of items 
        ///     depend on their position in the display.  For example, you can make
        ///     the background color of the items in ListBox alternate between 
        ///     blue and white.
        /// </remarks>
        [Bindable(true), CustomCategory("Content")]
        public int AlternationCount 
        {
            get { return (int) GetValue(AlternationCountProperty); } 
            set { SetValue(AlternationCountProperty, value); } 
        }
 
        /// <summary>
        ///     Called when AlternationCountProperty is invalidated on "d."
        /// </summary>
        private static void OnAlternationCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        {
            ItemsControl ctrl = (ItemsControl) d; 
 
            int oldAlternationCount = (int) e.OldValue;
            int newAlternationCount = (int) e.NewValue; 

            ctrl.OnAlternationCountChanged(oldAlternationCount, newAlternationCount);
        }
 
        /// <summary>
        ///     This method is invoked when the AlternationCount property changes. 
        /// </summary> 
        /// <param name="oldAlternationCount">The old value of the AlternationCount property.</param>
        /// <param name="newAlternationCount">The new value of the AlternationCount property.</param> 
        protected virtual void OnAlternationCountChanged(int oldAlternationCount, int newAlternationCount)
        {
            ItemContainerGenerator.ChangeAlternationCount();
        } 

        private static readonly DependencyPropertyKey AlternationIndexPropertyKey = 
                    DependencyProperty.RegisterAttachedReadOnly( 
                                "AlternationIndex",
                                typeof(int), 
                                typeof(ItemsControl),
                                new FrameworkPropertyMetadata((int)0));

        /// <summary> 
        /// AlternationIndex is set on containers generated for an ItemsControl, when
        /// the ItemsControl's AlternationCount property is positive.  The AlternationIndex 
        /// lies in the range [0, AlternationCount), and adjacent containers always get 
        /// assigned different values.
        /// </summary> 
        public static readonly DependencyProperty AlternationIndexProperty =
                    AlternationIndexPropertyKey.DependencyProperty;

        /// <summary> 
        /// Static getter for the AlternationIndex attached property.
        /// </summary> 
        public static int GetAlternationIndex(DependencyObject element) 
        {
            if (element == null) 
                throw new ArgumentNullException("element");

            return (int)element.GetValue(AlternationIndexProperty);
        } 

        // internal setter for AlternationIndex.  This property is not settable by 
        // an app, only by internal code 
        internal static void SetAlternationIndex(DependencyObject d, int value)
        { 
            d.SetValue(AlternationIndexPropertyKey, value);
        }

        // internal clearer for AlternationIndex.  This property is not settable by 
        // an app, only by internal code
        internal static void ClearAlternationIndex(DependencyObject d) 
        { 
            d.ClearValue(AlternationIndexPropertyKey);
        } 

        /// <summary>
        ///     The DependencyProperty for the IsTextSearchEnabled property.
        ///     Default Value:      false 
        /// </summary>
        public static readonly DependencyProperty IsTextSearchEnabledProperty = 
                DependencyProperty.Register( 
                        "IsTextSearchEnabled",
                        typeof(bool), 
                        typeof(ItemsControl),
                        new FrameworkPropertyMetadata(BooleanBoxes.FalseBox));

        /// <summary> 
        ///     Whether TextSearch is enabled or not on this ItemsControl
        /// </summary> 
        public bool IsTextSearchEnabled 
        {
            get { return (bool) GetValue(IsTextSearchEnabledProperty); } 
            set { SetValue(IsTextSearchEnabledProperty, BooleanBoxes.Box(value)); }
        }

        /// <summary> 
        ///     The DependencyProperty for the IsTextSearchCaseSensitive property.
        ///     Default Value:      false 
        /// </summary> 
        public static readonly DependencyProperty IsTextSearchCaseSensitiveProperty =
                DependencyProperty.Register( 
                        "IsTextSearchCaseSensitive",
                        typeof(bool),
                        typeof(ItemsControl),
                        new FrameworkPropertyMetadata(BooleanBoxes.FalseBox)); 

        /// <summary> 
        ///     Whether TextSearch is case sensitive or not on this ItemsControl 
        /// </summary>
        public bool IsTextSearchCaseSensitive 
        {
            get { return (bool) GetValue(IsTextSearchCaseSensitiveProperty); }
            set { SetValue(IsTextSearchCaseSensitiveProperty, BooleanBoxes.Box(value)); }
        } 
        #endregion
 
        #region Mapping methods 

        ///<summary> 
        /// Return the ItemsControl that owns the given container element
        ///</summary>
        public static ItemsControl ItemsControlFromItemContainer(DependencyObject container)
        { 
            UIElement ui = container as UIElement;
            if (ui == null) 
                return null; 

            // ui appeared in items collection 
            ItemsControl ic = LogicalTreeHelper.GetParent(ui) as ItemsControl;
            if (ic != null)
            {
                // this is the right ItemsControl as long as the item 
                // is (or is eligible to be) its own container
                IGeneratorHost host = ic as IGeneratorHost; 
                if (host.IsItemItsOwnContainer(ui)) 
                    return ic;
                else 
                    return null;
            }

            ui = VisualTreeHelper.GetParent(ui) as UIElement; 

            return ItemsControl.GetItemsOwner(ui); 
        } 

        ///<summary> 
        /// Return the container that owns the given element.  If itemsControl
        /// is not null, return a container that belongs to the given ItemsControl.
        /// If itemsControl is null, return the closest container belonging to
        /// any ItemsControl.  Return null if no such container exists. 
        ///</summary>
        public static DependencyObject ContainerFromElement(ItemsControl itemsControl, DependencyObject element) 
        { 
            if (element == null)
                throw new ArgumentNullException("element"); 

            // if the element is itself the desired container, return it
            if (IsContainerForItemsControl(element, itemsControl))
            { 
                return element;
            } 
 
            // start the tree walk at the element's parent
            FrameworkObject fo = new FrameworkObject(element); 
            fo.Reset(fo.GetPreferVisualParent(true).DO);

            // walk up, stopping when we reach the desired container
            while (fo.DO != null) 
            {
                if (IsContainerForItemsControl(fo.DO, itemsControl)) 
                { 
                    break;
                } 

                fo.Reset(fo.PreferVisualParent.DO);
            }
 
            return fo.DO;
        } 
 
        ///<summary>
        /// Return the container belonging to the current ItemsControl that owns 
        /// the given container element.  Return null if no such container exists.
        ///</summary>
        public DependencyObject ContainerFromElement(DependencyObject element)
        { 
            return ContainerFromElement(this, element);
        } 
 
        // helper method used by ContainerFromElement
        private static bool IsContainerForItemsControl(DependencyObject element, ItemsControl itemsControl) 
        {
            // is the element a container?
            if (element.ContainsValue(ItemContainerGenerator.ItemForItemContainerProperty))
            { 
                // does the element belong to the itemsControl?
                if (itemsControl == null || itemsControl == ItemsControlFromItemContainer(element)) 
                { 
                    return true;
                } 
            }

            return false;
        } 

        #endregion Mapping methods 
 
        #region IAddChild
 
        ///<summary>
        /// Called to Add the object as a Child.
        ///</summary>
        ///<param name="value"> 
        /// Object to add as a child
        ///</param> 
        void IAddChild.AddChild(Object value) 
        {
            AddChild(value); 
        }

        /// <summary>
        ///  Add an object child to this control 
        /// </summary>
        protected virtual void AddChild(object value) 
        { 
            Items.Add(value);
        } 

        ///<summary>
        /// Called when text appears under the tag in markup
        ///</summary> 
        ///<param name="text">
        /// Text to Add to the Object 
        ///</param> 
        void IAddChild.AddText(string text)
        { 
            AddText(text);
        }

        /// <summary> 
        ///  Add a text string to this control
        /// </summary> 
        protected virtual void AddText(string text) 
        {
            Items.Add(text); 
        }

        #endregion
 
        #region IGeneratorHost
 
        //----------------------------------------------------- 
        //
        //  Interface - IGeneratorHost 
        //
        //-----------------------------------------------------

        /// <summary> 
        /// The view of the data
        /// </summary> 
        ItemCollection IGeneratorHost.View 
        {
            get { return Items; } 
        }

        /// <summary>
        /// Return true if the item is (or is eligible to be) its own ItemContainer 
        /// </summary>
        bool IGeneratorHost.IsItemItsOwnContainer(object item) 
        { 
            return IsItemItsOwnContainerOverride(item);
        } 

        /// <summary>
        /// Return the element used to display the given item
        /// </summary> 
        DependencyObject IGeneratorHost.GetContainerForItem(object item)
        { 
            DependencyObject container; 

            // use the item directly, if possible (bug 870672) 
            if (IsItemItsOwnContainerOverride(item))
                container = item as DependencyObject;
            else
                container = GetContainerForItemOverride(); 

            // the container might have a parent from a previous 
            // generation (bug 873118).  If so, clean it up before using it again. 
            //
            // Note: This assumes the container is about to be added to a new parent, 
            // according to the ItemsControl/Generator/Container pattern.
            // If someone calls the generator and doesn't add the container to
            // a visual parent, unexpected things might happen.
            Visual visual = container as Visual; 
            if (visual != null)
            { 
                Visual parent = VisualTreeHelper.GetParent(visual) as Visual; 
                if (parent != null)
                { 
                    Invariant.Assert(parent is FrameworkElement, SR.Get(SRID.ItemsControl_ParentNotFrameworkElement));
                    Panel p = parent as Panel;
                    if (p != null && (visual is UIElement))
                    { 
                        p.Children.RemoveNoVerify((UIElement)visual);
                    } 
                    else 
                    {
                        ((FrameworkElement)parent).TemplateChild = null; 
                    }
                }
            }
 
            return container;
        } 
 
        /// <summary>
        /// Prepare the element to act as the ItemContainer for the corresponding item. 
        /// </summary>
        void IGeneratorHost.PrepareItemContainer(DependencyObject container, object item)
        {
            // GroupItems are special - their information comes from a different place 
            GroupItem groupItem = container as GroupItem;
            if (groupItem != null) 
            { 
                groupItem.PrepareItemContainer(item);
                return; 
            }

            if (ShouldApplyItemContainerStyle(container, item))
            { 
                // apply the ItemContainer style (if any)
                ApplyItemContainerStyle(container, item); 
            } 

            // forward ItemTemplate, et al. 
            PrepareContainerForItemOverride(container, item);

            // set up the binding group
            if (!Helper.HasUnmodifiedDefaultValue(this, ItemBindingGroupProperty) && 
                Helper.HasUnmodifiedDefaultOrInheritedValue(container, FrameworkElement.BindingGroupProperty))
            { 
                BindingGroup itemBindingGroup = ItemBindingGroup; 
                BindingGroup containerBindingGroup =
                    (itemBindingGroup != null)  ? new BindingGroup(itemBindingGroup) 
                                                : null;
                container.SetValue(FrameworkElement.BindingGroupProperty, containerBindingGroup);
            }
 
            if (container == item && TraceData.IsEnabled)
            { 
                // issue a message if there's an ItemTemplate(Selector) for "direct" items 
                // The ItemTemplate isn't used, which may confuse the user (bug 991101).
                if (ItemTemplate != null || ItemTemplateSelector != null) 
                {
                    TraceData.Trace(TraceEventType.Error, TraceData.ItemTemplateForDirectItem, AvTrace.TypeName(item));
                }
            } 

 
            // 
            // ItemValueStorage:  restore saved values for this item onto the new container
            // 
            // Note:  ItemValueStorage for now is only for TreeView.  In the future we could allow other types to use it.
            //
            if ((this is TreeViewItem || this is TreeView) && IsVirtualizing)
            { 
                SetItemValuesOnContainer(container, item, ItemValueStorageIndices);
            } 
        } 

        /// <summary> 
        /// Undo any initialization done on the element during GetContainerForItem and PrepareItemContainer
        /// </summary>
        void IGeneratorHost.ClearContainerForItem(DependencyObject container, object item)
        { 
            // This method no longer does most of the work it used to (bug 1445288).
            // It is called when a container is removed from the tree;  such a 
            // container will be GC'd soon, so there's no point in changing 
            // its properties.
            // 
            // We still call the override method, to give subclasses a chance
            // to clean up anything they may have done during Prepare (bug 1561206).

            GroupItem groupItem = container as GroupItem; 
            if (groupItem == null)
            { 
                ClearContainerForItemOverride(container, item); 
            }
            else 
            {
                // GroupItems are special - their information comes from a different place
                // Recursively clear the sub-generators, so that ClearContainerForItemOverride
                // is called on the bottom-level containers. 
                IItemContainerGenerator iicg = groupItem.Generator as IItemContainerGenerator;
                if (iicg != null) 
                { 
                    iicg.RemoveAll();
                } 
            }


            // 
            // ItemValueStorage:  save off values for this container if we're a virtualizing TreeView.
            // 
 
            //
            // Right now we have a hard-coded list of DPs we want to save off.  In the future we could provide a 'register' API 
            // so that each ItemsControl could decide what DPs to save on its containers. Maybe we define a virtual method to
            // retrieve a list of DPs the type is interested in.  Alternatively we could have the contract
            // be that ItemsControls use the ItemStorageService inside their ClearContainerForItemOverride by calling into StoreItemValues.
            // 

            // Would it be better to simply call the virtual and have each type decide which DPs it wants to save?  Doing these checks 
            // once per item is probably expensive. 
            if ((this is TreeViewItem || this is TreeView) && IsVirtualizing)
            { 

                // Tell the container to clear off all its containers.  This will cause this method to be called
                // recursively down the tree, allowing all descendent data to be stored before we save off
                // the ItemValueStorage DP for this container. 

                ItemsControl containerAsIC = container as ItemsControl; 
                if (containerAsIC != null && VirtualizingStackPanel.GetIsVirtualizing(container) == true) 
                {
                    VirtualizingStackPanel itemsHost = containerAsIC.ItemsHost as VirtualizingStackPanel; 

                    if (itemsHost != null)
                    {
                        itemsHost.ClearAllContainers(containerAsIC); 
                    }
                } 
 
                StoreItemValues(container, item, ItemValueStorageIndices);
            } 
        }


 
        /// <summary>
        /// Determine if the given element was generated for this host as an ItemContainer. 
        /// </summary> 
        bool IGeneratorHost.IsHostForItemContainer(DependencyObject container)
        { 
            // If ItemsControlFromItemContainer can determine who owns the element,
            // use its decision.
            ItemsControl ic = ItemsControlFromItemContainer(container);
            if (ic != null) 
                return (ic == this);
 
            // If the element is in my items view, and if it can be its own ItemContainer, 
            // it's mine.  Contains may be expensive, so we avoid calling it in cases
            // where we already know the answer - namely when the element has a 
            // logical parent (ItemsControlFromItemContainer handles this case).  This
            // leaves only those cases where the element belongs to my items
            // without having a logical parent (e.g. via ItemsSource) and without
            // having been generated yet. HasItem indicates if anything has been generated. 
            DependencyObject parent = LogicalTreeHelper.GetParent(container);
            if (parent == null) 
            { 
                return IsItemItsOwnContainerOverride(container) &&
                    HasItems && Items.Contains(container); 
            }

            // Otherwise it's not mine
            return false; 
        }
 
        /// <summary> 
        /// Return the GroupStyle (if any) to use for the given group at the given level.
        /// </summary> 
        GroupStyle IGeneratorHost.GetGroupStyle(CollectionViewGroup group, int level)
        {
            GroupStyle result = null;
 
            // a. Use global selector
            if (GroupStyleSelector != null) 
            { 
                result = GroupStyleSelector(group, level);
            } 

            // b. lookup in GroupStyle list
            if (result == null)
            { 
                // use last entry for all higher levels
                if (level >= GroupStyle.Count) 
                { 
                    level = GroupStyle.Count - 1;
                } 

                if (level >= 0)
                {
                    result = GroupStyle[level]; 
                }
            } 
 
            return result;
        } 

        /// <summary>
        /// Communicates to the host that the generator is using grouping.
        /// </summary> 
        void IGeneratorHost.SetIsGrouping(bool isGrouping)
        { 
            SetValue(IsGroupingPropertyKey, BooleanBoxes.Box(isGrouping)); 
        }
 
        /// <summary>
        /// The AlternationCount
        /// <summary>
        int IGeneratorHost.AlternationCount { get { return AlternationCount; } } 

        #endregion IGeneratorHost 
 
        #region ISupportInitialize
        /// <summary> 
        ///     Initialization of this element is about to begin
        /// </summary>
        public override void BeginInit()
        { 
            base.BeginInit();
 
            if (_items != null) 
            {
                _items.BeginInit(); 
            }
        }

        /// <summary> 
        ///     Initialization of this element has completed
        /// </summary> 
        public override void EndInit() 
        {
            if (IsInitPending) 
            {
                if (_items != null)
                {
                    _items.EndInit(); 
                }
 
                base.EndInit(); 
            }
        } 

        private bool IsInitPending
        {
            get 
            {
                return ReadInternalFlag(InternalFlags.InitPending); 
            } 
        }
 
        #endregion

        #region Protected Methods
 
        /// <summary>
        /// Return true if the item is (or should be) its own item container 
        /// </summary> 
        protected virtual bool IsItemItsOwnContainerOverride(object item)
        { 
            return (item is UIElement);
        }

        /// <summary> Create or identify the element used to display the given item. </summary> 
        protected virtual DependencyObject GetContainerForItemOverride()
        { 
            return new ContentPresenter(); 
        }
 
        /// <summary>
        /// Prepare the element to display the item.  This may involve
        /// applying styles, setting bindings, etc.
        /// </summary> 
        protected virtual void PrepareContainerForItemOverride(DependencyObject element, object item)
        { 
            // Each type of "ItemContainer" element may require its own initialization. 
            // We use explicit polymorphism via internal methods for this.
            // 
            // Another way would be to define an interface IGeneratedItemContainer with
            // corresponding virtual "core" methods.  Base classes (ContentControl,
            // ItemsControl, ContentPresenter) would implement the interface
            // and forward the work to subclasses via the "core" methods. 
            //
            // While this is better from an OO point of view, and extends to 
            // 3rd-party elements used as containers, it exposes more public API. 
            // Management considers this undesirable, hence the following rather
            // inelegant code. 

            HeaderedContentControl hcc;
            ContentControl cc;
            ContentPresenter cp; 
            ItemsControl ic;
            HeaderedItemsControl hic; 
 
            if ((hcc = element as HeaderedContentControl) != null)
            { 
                hcc.PrepareHeaderedContentControl(item, ItemTemplate, ItemTemplateSelector, ItemStringFormat);
            }
            else if ((cc = element as ContentControl) != null)
            { 
                cc.PrepareContentControl(item, ItemTemplate, ItemTemplateSelector, ItemStringFormat);
            } 
            else if ((cp = element as ContentPresenter) != null) 
            {
                cp.PrepareContentPresenter(item, ItemTemplate, ItemTemplateSelector, ItemStringFormat); 
            }
            else if ((hic = element as HeaderedItemsControl) != null)
            {
                hic.PrepareHeaderedItemsControl(item, this); 
            }
            else if ((ic = element as ItemsControl) != null) 
            { 
                if (ic != this)
                { 
                    ic.PrepareItemsControl(item, this);
                }
            }
        } 

        /// <summary> 
        /// Undo the effects of PrepareContainerForItemOverride. 
        /// </summary>
        protected virtual void ClearContainerForItemOverride(DependencyObject element, object item) 
        {
            HeaderedContentControl hcc;
            ContentControl cc;
            ContentPresenter cp; 
            ItemsControl ic;
            HeaderedItemsControl hic; 
 
            if ((hcc = element as HeaderedContentControl) != null)
            { 
                hcc.ClearHeaderedContentControl(item);
            }
            else if ((cc = element as ContentControl) != null)
            { 
                cc.ClearContentControl(item);
            } 
            else if ((cp = element as ContentPresenter) != null) 
            {
                cp.ClearContentPresenter(item); 
            }
            else if ((hic = element as HeaderedItemsControl) != null)
            {
                hic.ClearHeaderedItemsControl(item); 
            }
            else if ((ic = element as ItemsControl) != null) 
            { 
                if (ic != this)
                { 
                    ic.ClearItemsControl(item);
                }
            }
        } 

        /// <summary> 
        ///     Called when a TextInput event is received. 
        /// </summary>
        /// <param name="e"></param> 
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);
 
            // Only handle text from ourselves or an item container
            if (!String.IsNullOrEmpty(e.Text) && IsTextSearchEnabled && 
                (e.OriginalSource == this || ItemsControlFromItemContainer(e.OriginalSource as DependencyObject) == this)) 
            {
                TextSearch instance = TextSearch.EnsureInstance(this); 

                if (instance != null)
                {
                    instance.DoSearch(e.Text); 
                    // Note: we always want to handle the event to denote that we
                    // actually did something.  We wouldn't want an AccessKey 
                    // to get invoked just because there wasn't a match here. 
                    e.Handled = true;
                } 
            }
        }

        /// <summary> 
        ///     Called when a KeyDown event is received.
        /// </summary> 
        /// <param name="e"></param> 
        protected override void OnKeyDown(KeyEventArgs e)
        { 
            base.OnKeyDown(e);
            if (IsTextSearchEnabled)
            {
                // If the pressed the backspace key, delete the last character 
                // in the TextSearch current prefix.
                if (e.Key == Key.Back) 
                { 
                    TextSearch instance = TextSearch.EnsureInstance(this);
 
                    if (instance != null)
                    {
                        instance.DeleteLastCharacter();
                    } 
                }
            } 
        } 

        internal override void OnTemplateChangedInternal(FrameworkTemplate oldTemplate, FrameworkTemplate newTemplate) 
        {
            // Forget about the old ItemsHost we had when the style changes
            _itemsHost = null;
            _scrollHost = null; 
            WriteControlFlag(ControlBoolFlags.ScrollHostValid, false);
 
            base.OnTemplateChangedInternal(oldTemplate, newTemplate); 
        }
 
        /// <summary>
        /// Determine whether the ItemContainerStyle/StyleSelector should apply to the container
        /// </summary>
        /// <returns>true if the ItemContainerStyle should apply to the item</returns> 
        protected virtual bool ShouldApplyItemContainerStyle(DependencyObject container, object item)
        { 
            return true; 
        }
 
        #endregion

        //------------------------------------------------------
        // 
        //  Internal methods
        // 
        //----------------------------------------------------- 

        #region Internal Methods 

        /// <summary>
        /// Prepare to display the item.
        /// </summary> 
        internal void PrepareItemsControl(object item, ItemsControl parentItemsControl)
        { 
            if (item != this) 
            {
                // copy templates and styles from parent ItemsControl 
                DataTemplate itemTemplate = parentItemsControl.ItemTemplate;
                DataTemplateSelector itemTemplateSelector = parentItemsControl.ItemTemplateSelector;
                string itemStringFormat = parentItemsControl.ItemStringFormat;
                Style itemContainerStyle = parentItemsControl.ItemContainerStyle; 
                StyleSelector itemContainerStyleSelector = parentItemsControl.ItemContainerStyleSelector;
                int alternationCount = parentItemsControl.AlternationCount; 
                BindingGroup itemBindingGroup = parentItemsControl.ItemBindingGroup; 

                if (itemTemplate != null) 
                {
                    SetValue(ItemTemplateProperty, itemTemplate);
                }
                if (itemTemplateSelector != null) 
                {
                    SetValue(ItemTemplateSelectorProperty, itemTemplateSelector); 
                } 
                if (itemStringFormat != null &&
                    Helper.HasDefaultValue(this, ItemStringFormatProperty)) 
                {
                    SetValue(ItemStringFormatProperty, itemStringFormat);
                }
                if (itemContainerStyle != null && 
                    Helper.HasDefaultValue(this, ItemContainerStyleProperty))
                { 
                    SetValue(ItemContainerStyleProperty, itemContainerStyle); 
                }
                if (itemContainerStyleSelector != null && 
                    Helper.HasDefaultValue(this, ItemContainerStyleSelectorProperty))
                {
                    SetValue(ItemContainerStyleSelectorProperty, itemContainerStyleSelector);
                } 
                if (alternationCount != 0 &&
                    Helper.HasDefaultValue(this, AlternationCountProperty)) 
                { 
                    SetValue(AlternationCountProperty, alternationCount);
                } 
                if (itemBindingGroup != null &&
                    Helper.HasDefaultValue(this, ItemBindingGroupProperty))
                {
                    SetValue(ItemBindingGroupProperty, itemBindingGroup); 
                }
            } 
        } 

        /// <summary> 
        /// Undo the effect of PrepareItemsControl.
        /// </summary>
        internal void ClearItemsControl(object item)
        { 
            if (item != this)
            { 
                // nothing to do 
            }
        } 

        /// <summary>
        /// Bringing the item passed as arg into view. If item is virtualized it will become realized.
        /// </summary> 
        /// <param name="arg"></param>
        /// <returns></returns> 
        internal object OnBringItemIntoView(object arg) 
        {
            FrameworkElement element = ItemContainerGenerator.ContainerFromItem(arg) as FrameworkElement; 
            if (element != null)
            {
                element.BringIntoView();
            } 
            else if (!IsGrouping && Items.Contains(arg))
            { 
                // We might be virtualized, try to de-virtualize the item. 
                // Note: There is opportunity here to make a public OM.
 
                VirtualizingPanel itemsHost = ItemsHost as VirtualizingPanel;
                if (itemsHost != null)
                {
                    itemsHost.BringIndexIntoView(Items.IndexOf(arg)); 
                }
            } 
 
            return null;
        } 

        internal Panel ItemsHost
        {
            get 
            {
                return _itemsHost; 
            } 
            set { _itemsHost = value; }
        } 


        internal bool IsVirtualizing
        { 
            get
            { 
                return VirtualizingStackPanel.GetIsVirtualizing(this); 
            }
        } 

        #region Keyboard Navigation

        internal void NavigateByLine(FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs) 
        {
            NavigateByLine(FocusedItem, direction, itemNavigateArgs); 
        } 

        internal void NavigateByLine(object startingItem, FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs) 
        {
            if (ItemsHost == null)
            {
                return; 
            }
 
            // If the focused item has been scrolled out of view and they want to 
            // start navigating again, scroll it back into view.
            if (startingItem != null && !IsOnCurrentPage(startingItem, direction)) 
            {
                MakeVisible(Items.IndexOf(startingItem));
                // Wait for layout
                ItemsHost.UpdateLayout(); 
            }
 
            // When we get here if startingItem is non-null, it must be on the visible page. 
            NavigateByLineInternal(startingItem, direction, itemNavigateArgs);
        } 

        private void NavigateByLineInternal(object startingItem, FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs)
        {
            // If there is no starting item, just navigate to the first item. 
            if (startingItem == null)
            { 
                NavigateToStart(itemNavigateArgs); 
            }
            else 
            {
                FrameworkElement startingElement = null;
                FrameworkElement nextElement = null;
 
                startingElement = ItemContainerGenerator.ContainerFromItem(startingItem) as FrameworkElement;
                // If the container isn't there, it might have been degenerated or 
                // it might have been scrolled out of view.  Either way, we 
                // should start navigation from the ItemsHost b/c we know it
                // is visible. 
                // The generator could have given us an element which isn't
                // actually visually connected.  In this case we should use
                // the ItemsHost as well.
                if (startingElement == null || !ItemsHost.IsAncestorOf(startingElement)) 
                {
                    // Bug 991220 makes it so that we have to start from the ScrollHost. 
                    // If we try to start from the ItemsHost it will always skip the first item. 
                    startingElement = ScrollHost;
                } 

                nextElement = KeyboardNavigation.Current.PredictFocusedElement(startingElement, direction) as FrameworkElement;

                // We can only navigate there if the target element is in the items host. 
                if ((nextElement != null) && (ItemsHost.IsAncestorOf(nextElement)))
                { 
                    object nextItem = GetEncapsulatingItem(nextElement); 

                    if (nextItem != DependencyProperty.UnsetValue) 
                    {
                        NavigateToItem(nextItem, itemNavigateArgs);
                    }
                } 
            }
        } 
 
        internal void NavigateByPage(FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs)
        { 
            NavigateByPage(FocusedItem, direction, itemNavigateArgs);
        }

        internal void NavigateByPage(object startingItem, FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs) 
        {
            if (ItemsHost == null) 
            { 
                return;
            } 

            // If the focused item has been scrolled out of view and they want to
            // start navigating again, scroll it back into view.
            if (startingItem != null && !IsOnCurrentPage(startingItem, direction)) 
            {
                while (MakeVisible(Items.IndexOf(startingItem))) 
                { 
                    double oldHorizontalOffset = ScrollHost.HorizontalOffset;
                    double oldVerticalOffset = ScrollHost.VerticalOffset; 

                    ItemsHost.UpdateLayout();

                    // If offset does not change - exit the loop 
                    if (DoubleUtil.AreClose(oldHorizontalOffset, ScrollHost.HorizontalOffset) &&
                        DoubleUtil.AreClose(oldVerticalOffset, ScrollHost.VerticalOffset)) 
                        break; 
                }
            } 

            NavigateByPageInternal(startingItem, direction, itemNavigateArgs);
        }
 
        private void NavigateByPageInternal(object startingItem, FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs)
        { 
            // Move to the last guy on the page if we're not already there. 
            if (startingItem == null)
            { 
                NavigateToFirstItemOnCurrentPage(startingItem, direction, itemNavigateArgs);
            }
            else
            { 
                // See if the currently focused guy is the first or last one one the page
                int firstIndex; 
                object first = GetFirstItemOnCurrentPage(startingItem, direction, out firstIndex); 

                if (startingItem.Equals(first)) 
                {
                    // Page in that direction
                    bool navigateAfterMeasure = false;
 
                    if (ScrollHost != null)
                    { 
                        switch (direction) 
                        {
                            case FocusNavigationDirection.Up: 
                                if (IsLogicalHorizontal)
                                {
                                    ScrollHost.PageLeft();
                                } 
                                else
                                { 
                                    ScrollHost.PageUp(); 
                                }
 
                                navigateAfterMeasure = true;
                                break;

                            case FocusNavigationDirection.Down: 
                                if (IsLogicalHorizontal)
                                { 
                                    ScrollHost.PageRight(); 
                                }
                                else 
                                {
                                    ScrollHost.PageDown();
                                }
 
                                navigateAfterMeasure = true;
                                break; 
                        } 
                    }
 
                    // After measure we should focus the first guy on the page
                    if (navigateAfterMeasure)
                    {
                        if (ItemsHost != null) 
                        {
                            ItemsHost.UpdateLayout(); 
                            NavigateToFirstItemOnCurrentPage(startingItem, direction, itemNavigateArgs); 
                        }
                    } 
                }
                else
                {
                    // The currently focused guy is not the first on the page, so move there 
                    if (first != DependencyProperty.UnsetValue)
                    { 
                        NavigateToItem(first, firstIndex, itemNavigateArgs); 
                    }
                } 
            }
        }

        internal void NavigateToStart(ItemNavigateArgs itemNavigateArgs) 
        {
            if (HasItems) 
            { 
                int foundIndex;
                object item = FindFocusable(0, 1, out foundIndex); 
                NavigateToItem(item, foundIndex, itemNavigateArgs);
            }
        }
 
        internal void NavigateToEnd(ItemNavigateArgs itemNavigateArgs)
        { 
            if (HasItems) 
            {
                int foundIndex; 
                object item = FindFocusable(Items.Count - 1, -1, out foundIndex);
                NavigateToItem(item, foundIndex, itemNavigateArgs);
            }
        } 

        internal void NavigateToItem(object item, ItemNavigateArgs itemNavigateArgs) 
        { 
            NavigateToItem(item, -1, itemNavigateArgs, false /* alwaysAtTopOfViewport */);
        } 

        internal void NavigateToItem(object item, int itemIndex, ItemNavigateArgs itemNavigateArgs)
        {
            NavigateToItem(item, itemIndex, itemNavigateArgs, false /* alwaysAtTopOfViewport */); 
        }
 
        internal void NavigateToItem(object item, ItemNavigateArgs itemNavigateArgs, bool alwaysAtTopOfViewport) 
        {
            NavigateToItem(item, -1, itemNavigateArgs, alwaysAtTopOfViewport); 
        }

        private void NavigateToItem(object item, int elementIndex, ItemNavigateArgs itemNavigateArgs, bool alwaysAtTopOfViewport)
        { 
            //
 
            // Perhaps the container isn't generated yet.  In this case we try to shift the view, 
            // wait for measure, and then call it again.
            if (item == DependencyProperty.UnsetValue) 
            {
                return;
            }
 
            if (elementIndex == -1)
            { 
                elementIndex = Items.IndexOf(item); 
                if (elementIndex == -1)
                    return; 
            }

            while (MakeVisible(elementIndex, alwaysAtTopOfViewport, false /* alignMinorAxisToo */))
            { 
                // The above operations to change VerticalOffset might have invalidated measure.
                // Try again after measure. 
                Debug.Assert(ItemsHost != null); 

 
                double oldHorizontalOffset = ScrollHost.HorizontalOffset;
                double oldVerticalOffset = ScrollHost.VerticalOffset;

                ItemsHost.UpdateLayout(); 

                // If offset does not change - exit the loop 
                if (DoubleUtil.AreClose(oldHorizontalOffset, ScrollHost.HorizontalOffset) && 
                    DoubleUtil.AreClose(oldVerticalOffset, ScrollHost.VerticalOffset))
                    break; 
            }

            FocusItem(item, itemNavigateArgs);
        } 

        private object FindFocusable(int startIndex, int direction, out int foundIndex) 
        { 
            // HasItems may be wrong when underlying collection does not notify, but this function
            // only cares about what's been generated and is consistent with ItemsControl state. 
            if (HasItems)
            {
                int count = Items.Count;
                for (; startIndex >= 0 && startIndex < count; startIndex += direction) 
                {
                    FrameworkElement container = ItemContainerGenerator.ContainerFromIndex(startIndex) as FrameworkElement; 
 
                    // If the UI is non-null it must meet some minimum requirements to consider it for
                    // navigation (focusable, enabled).  If it has no UI we can make no judgements about it 
                    // at this time, so it is navigable.
                    if (container == null || Keyboard.IsFocusable(container))
                    {
                        foundIndex = startIndex; 
                        return Items[startIndex];
                    } 
                } 
            }
 
            foundIndex = -1;
            return null;
        }
 
        private bool MakeVisible(int index)
        { 
            return MakeVisible(index, false /* alwaysAtTopOfViewport */, false /* alignMinorAxisToo */); 
        }
 
        // Shifts the viewport to make the given index visible.
        // Returns true if the viewport shifted.
        internal bool MakeVisible(int index, bool alwaysAtTopOfViewport, bool alignMinorAxisToo)
        { 
            if (index == -1) return false;
 
            if (ScrollHost != null) 
            {
                bool offsetChanged = false; 

                double initialHorizontalOffset = ScrollHost.HorizontalOffset;
                double initialVerticalOffset = ScrollHost.VerticalOffset;
 
                double newHorizontalOffset = initialHorizontalOffset;
                double newVerticalOffset = initialVerticalOffset; 
 
                if (IsLogicalVertical)
                { 
                    if (alwaysAtTopOfViewport)
                    {
                        newVerticalOffset = index;
                    } 
                    else
                    { 
                        // First check that the bottom is visible 
                        if (DoubleUtil.GreaterThan(index + 1, initialVerticalOffset + ScrollHost.ViewportHeight))
                        { 
                            newVerticalOffset = Math.Max(0.0, index + 1 - ScrollHost.ViewportHeight);
                        }

                        // Next make sure that the top is visible 
                        if (DoubleUtil.LessThan(index, initialVerticalOffset))
                        { 
                            newVerticalOffset = index; 
                        }
                    } 

                    if (alignMinorAxisToo)
                    {
                        newHorizontalOffset = 0; 
                    }
 
                    if (!DoubleUtil.AreClose(initialHorizontalOffset, newHorizontalOffset)) 
                    {
                        ScrollHost.ScrollToHorizontalOffset(newHorizontalOffset); 
                        offsetChanged = true;
                    }

                    if (!DoubleUtil.AreClose(initialVerticalOffset, newVerticalOffset)) 
                    {
                        ScrollHost.ScrollToVerticalOffset(newVerticalOffset); 
                        offsetChanged = true; 
                    }
                } 
                else if (IsLogicalHorizontal)
                {
                    if (alwaysAtTopOfViewport)
                    { 
                        newHorizontalOffset = index;
                    } 
                    else 
                    {
                        // First check that the bottom is visible 
                        if (DoubleUtil.GreaterThan(index + 1, initialHorizontalOffset + ScrollHost.ViewportWidth))
                        {
                            newHorizontalOffset = Math.Max(0.0, index + 1 - ScrollHost.ViewportWidth);
                        } 

                        // Next make sure that the top is visible 
                        if (DoubleUtil.LessThan(index, initialHorizontalOffset)) 
                        {
                            newHorizontalOffset = index; 
                        }
                    }

                    if (alignMinorAxisToo) 
                    {
                        newVerticalOffset = 0; 
                    } 

                    if (!DoubleUtil.AreClose(initialHorizontalOffset, newHorizontalOffset)) 
                    {
                        ScrollHost.ScrollToHorizontalOffset(newHorizontalOffset);
                        offsetChanged = true;
                    } 

                    if (!DoubleUtil.AreClose(initialVerticalOffset, newVerticalOffset)) 
                    { 
                        ScrollHost.ScrollToVerticalOffset(newVerticalOffset);
                        offsetChanged = true; 
                    }
                }
                else
                { 
                    FrameworkElement container = ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                    if (container != null) 
                    { 
                        container.BringIntoView();
                        offsetChanged = !DoubleUtil.AreClose(initialHorizontalOffset, ScrollHost.HorizontalOffset) || 
                                        !DoubleUtil.AreClose(initialVerticalOffset, ScrollHost.VerticalOffset);
                    }
                }
 
                return offsetChanged;
            } 
 
            return false;
        } 

        private void NavigateToFirstItemOnCurrentPage(object startingItem, FocusNavigationDirection direction, ItemNavigateArgs itemNavigateArgs)
        {
            int foundIndex; 
            object firstItem = GetFirstItemOnCurrentPage(startingItem, direction, out foundIndex);
 
            if (firstItem != DependencyProperty.UnsetValue) 
            {
                FocusItem(firstItem, itemNavigateArgs); 
            }
        }

        private object GetFirstItemOnCurrentPage(object startingItem, FocusNavigationDirection direction, out int foundIndex) 
        {
            Debug.Assert(direction == FocusNavigationDirection.Up || direction == FocusNavigationDirection.Down, "Can only get the first item on a page using North or South"); 
            foundIndex = -1; 

            // 
            if (IsLogicalVertical)
            {
                if (direction == FocusNavigationDirection.Up)
                { 
                    return FindFocusable((int)ScrollHost.VerticalOffset, 1, out foundIndex);
                } 
                else // if (direction == FocusNavigationDirection.Down) 
                {
                    return FindFocusable((int)(ScrollHost.VerticalOffset + ScrollHost.ViewportHeight - 1), -1, out foundIndex); 
                }
            }
            else if (IsLogicalHorizontal)
            { 
                if (direction == FocusNavigationDirection.Up)
                { 
                    return FindFocusable((int)ScrollHost.HorizontalOffset, 1, out foundIndex); 
                }
                else // if (direction == FocusNavigationDirection.Down) 
                {
                    return FindFocusable((int)(ScrollHost.HorizontalOffset + ScrollHost.ViewportWidth - 1), -1, out foundIndex);
                }
            } 

            // We assume we're physically scrolling in both directions now. 
            FrameworkElement startElement = ItemContainerGenerator.ContainerFromItem(startingItem) as FrameworkElement; 
            FrameworkElement currentElement = startElement;
            FrameworkElement previousElement = null; 
            if (startElement != null)
            {
                // If the focused guy isn't on the page, try to move until we are on the page.
                // ISSUE: KeyboardNavigation needs to incorporate this logic instead this workaround. 
                while (currentElement != null && !IsOnCurrentPage(currentElement, direction))
                { 
                    previousElement = currentElement; 
                    currentElement = KeyboardNavigation.Current.PredictFocusedElement(currentElement, direction) as FrameworkElement;
                } 

                while (currentElement != null && IsOnCurrentPage(currentElement, direction))
                {
                    previousElement = currentElement; 
                    currentElement = KeyboardNavigation.Current.PredictFocusedElement(currentElement, direction) as FrameworkElement;
                } 
 
                return GetEncapsulatingItem(previousElement);
            } 

            return null;
        }
 
        /// <summary>
        /// Determines if the given item is on the current visible page. 
        /// </summary> 
        private bool IsOnCurrentPage(object item, FocusNavigationDirection axis)
        { 
            FrameworkElement container = ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;

            if (container == null)
            { 
                return false;
            } 
 
            return IsOnCurrentPage(container, axis, false);
        } 

        private bool IsOnCurrentPage(FrameworkElement element, FocusNavigationDirection axis)
        {
            return IsOnCurrentPage(element, axis, false); 
        }
 
        /// <summary> 
        /// Determines if the given element is on the current visible page.
        /// The element must be completely on the page on the given axis, but need 
        /// not be completely contained on the page in the perpendicular axis.
        /// For example, if axis == North, then the element's Top and Bottom must
        /// be completely contained on the page.
        /// </summary> 
        private bool IsOnCurrentPage(FrameworkElement element, FocusNavigationDirection axis, bool fullyVisible)
        { 
            // NOTE: When ScrollHost is non-null, we use ScrollHost instead of 
            //       ItemsHost because ItemsHost in the physically scrolling
            //       case will just have its layout offset shifted, and all 
            //       items will always be within the bounding box of the ItemsHost,
            //       and we want to know if you can actually see the element.
            FrameworkElement viewPort = ScrollHost;
            if (viewPort == null) 
            {
                viewPort = ItemsHost; 
            } 

            // If there's no ScrollHost or ItemsHost, the element is not on the page 
            if (viewPort == null)
            {
                return false;
            } 

            if (element == null || !viewPort.IsAncestorOf(element)) 
            { 
                return false;
            } 

            Rect viewPortBounds = new Rect(new Point(), viewPort.RenderSize);
            Rect elementBounds = new Rect(new Point(), element.RenderSize);
            elementBounds = element.TransformToAncestor(viewPort).TransformBounds(elementBounds); 

            // Return true if the element is completely contained within the page along the given axis. 
 
            if (fullyVisible)
            { 
                return viewPortBounds.Contains(elementBounds);
            }
            else
            { 
                if (axis == FocusNavigationDirection.Up || axis == FocusNavigationDirection.Down)
                { 
                    // Check that the element's Top/Bottom are inside the viewport's top and bottom 
                    if (DoubleUtil.LessThanOrClose(viewPortBounds.Top, elementBounds.Top)
                        && DoubleUtil.LessThanOrClose(elementBounds.Bottom, viewPortBounds.Bottom)) 
                    {
                        return true;
                    }
                } 
                else if (axis == FocusNavigationDirection.Right || axis == FocusNavigationDirection.Left)
                { 
                    if (DoubleUtil.LessThanOrClose(viewPortBounds.Left, elementBounds.Left) 
                        && DoubleUtil.LessThanOrClose(elementBounds.Right, viewPortBounds.Right))
                    { 
                        return true;
                    }
                }
            } 

 
            return false; 
        }
 
        private static void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ItemsControl itemsControl = (ItemsControl)sender;
            UIElement itemContainer = e.OriginalSource as UIElement; 
            if ((itemContainer != null) && (itemContainer != itemsControl))
            { 
                object item = itemsControl.ItemContainerGenerator.ItemFromContainer(itemContainer); 
                if (item != DependencyProperty.UnsetValue)
                    itemsControl._focusedItem = item; 
            }
        }

 
        /// <summary>
        /// The item corresponding to the UI container which has focus. 
        /// Virtualizing panels remove visual children you can't see. 
        /// When you scroll the focused element out of view we throw
        /// focus back on to the items control and remember the item which 
        /// was focused.  When it scrolls back into view (and focus is
        /// still on the ItemsControl) we'll focus it.
        /// </summary>
        internal object FocusedItem 
        {
            get { return _focusedItem; } 
        } 

        private object _focusedItem; 

        internal class ItemNavigateArgs
        {
            public ItemNavigateArgs(InputDevice deviceUsed, ModifierKeys modifierKeys) 
            {
                _deviceUsed = deviceUsed; 
                _modifierKeys = modifierKeys; 
            }
 
            public InputDevice DeviceUsed { get { return _deviceUsed; } }

            private InputDevice _deviceUsed;
            private ModifierKeys _modifierKeys; 

            public static ItemNavigateArgs Empty 
            { 
                get
                { 
                    if (_empty == null)
                    {
                        _empty = new ItemNavigateArgs(null, ModifierKeys.None);;
                    } 
                    return _empty;
                } 
            } 
            private static ItemNavigateArgs _empty;
        } 

        //
        internal virtual void FocusItem(object item, ItemNavigateArgs itemNavigateArgs)
        { 
            if (item != null)
            { 
                UIElement container = ItemContainerGenerator.ContainerFromItem(item) as UIElement; 
                if (container != null)
                { 
                    Keyboard.Focus(container);
                }
            }
            if (itemNavigateArgs.DeviceUsed is KeyboardDevice) 
            {
                KeyboardNavigation.ShowFocusVisual(); 
            } 
        }
 
        // ISSUE: IsLogicalVertical and IsLogicalHorizontal are rough guesses as to whether
        //        the ItemsHost is virtualizing in a particular direction.  Ideally this
        //        would be exposed through the IScrollInfo.
 

        internal bool IsLogicalVertical 
        { 
            get
            { 
                return (ItemsHost != null && ItemsHost.HasLogicalOrientation && ItemsHost.LogicalOrientation == Orientation.Vertical &&
                        ScrollHost != null && ScrollHost.CanContentScroll);
            }
        } 

        internal bool IsLogicalHorizontal 
        { 
            get
            { 
                return (ItemsHost != null && ItemsHost.HasLogicalOrientation && ItemsHost.LogicalOrientation == Orientation.Horizontal &&
                        ScrollHost != null && ScrollHost.CanContentScroll);
            }
        } 

        internal ScrollViewer ScrollHost 
        { 
            get
            { 
                if (!ReadControlFlag(ControlBoolFlags.ScrollHostValid))
                {
                    if (_itemsHost == null)
                    { 
                        return null;
                    } 
                    else 
                    {
                        // We have an itemshost, so walk up the tree looking for the ScrollViewer 
                        for (DependencyObject current = _itemsHost; current != this && current != null; current = VisualTreeHelper.GetParent(current))
                        {
                            ScrollViewer scrollViewer = current as ScrollViewer;
                            if (scrollViewer != null) 
                            {
                                _scrollHost = scrollViewer; 
                                break; 
                            }
                        } 

                        WriteControlFlag(ControlBoolFlags.ScrollHostValid, true);
                    }
                } 

                return _scrollHost; 
            } 
        }
 
        internal static TimeSpan AutoScrollTimeout
        {
            get
            { 
                // NOTE: NtUser does the following (file: windows/ntuser/kernel/sysmet.c)
                //     gpsi->dtLBSearch = dtTime * 4;            // dtLBSearch   =  4  * gdtDblClk 
                //     gpsi->dtScroll = gpsi->dtLBSearch / 5;  // dtScroll     = 4/5 * gdtDblClk 

                return TimeSpan.FromMilliseconds(MS.Win32.SafeNativeMethods.GetDoubleClickTime() * 0.8); 
            }
        }

        internal void DoAutoScroll() 
        {
            DoAutoScroll(FocusedItem); 
        } 

        internal void DoAutoScroll(object startingItem) 
        {
            // Attempt to compute positions based on the ScrollHost.
            // If that doesn't exist, use the ItemsHost.
            FrameworkElement relativeTo = ScrollHost != null ? (FrameworkElement)ScrollHost : ItemsHost; 
            if (relativeTo != null)
            { 
                // Figure out where the mouse is w.r.t. the ItemsControl. 

                Point mousePosition = Mouse.GetPosition(relativeTo); 

                // Take the bounding box of the ListBox and scroll against that
                Rect bounds = new Rect(new Point(), relativeTo.RenderSize);
                bool focusChanged = false; 

                if (mousePosition.Y < bounds.Top) 
                { 
                    NavigateByLine(startingItem, FocusNavigationDirection.Up, new ItemNavigateArgs(Mouse.PrimaryDevice, Keyboard.Modifiers));
                    focusChanged = startingItem != FocusedItem; 
                }
                else if (mousePosition.Y >= bounds.Bottom)
                {
                    NavigateByLine(startingItem, FocusNavigationDirection.Down, new ItemNavigateArgs(Mouse.PrimaryDevice, Keyboard.Modifiers)); 
                    focusChanged = startingItem != FocusedItem;
                } 
 
                // Try horizontal scroll if vertical scroll did not happen
                if (!focusChanged) 
                {
                    if (mousePosition.X < bounds.Left)
                    {
                        FocusNavigationDirection direction = FocusNavigationDirection.Left; 
                        if (IsRTL(relativeTo))
                        { 
                            direction = FocusNavigationDirection.Right; 
                        }
 
                        NavigateByLine(startingItem, direction, new ItemNavigateArgs(Mouse.PrimaryDevice, Keyboard.Modifiers));
                    }
                    else if (mousePosition.X >= bounds.Right)
                    { 
                        FocusNavigationDirection direction = FocusNavigationDirection.Right;
                        if (IsRTL(relativeTo)) 
                        { 
                            direction = FocusNavigationDirection.Left;
                        } 

                        NavigateByLine(startingItem, direction, new ItemNavigateArgs(Mouse.PrimaryDevice, Keyboard.Modifiers));
                    }
                } 
            }
        } 
 
        private bool IsRTL(FrameworkElement element)
        { 
            FlowDirection flowDirection = element.FlowDirection;
            return (flowDirection == FlowDirection.RightToLeft);
        }
 
        private object GetEncapsulatingItem(FrameworkElement element)
        { 
            object item = DependencyProperty.UnsetValue; 

            while (item == DependencyProperty.UnsetValue && element != null) 
            {
                item = ItemContainerGenerator.ItemFromContainer(element);
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            } 

            return item; 
        } 

        #endregion Keyboard Navigation 

        #endregion

        //------------------------------------------------------ 
        //
        //  Private Methods 
        // 
        //------------------------------------------------------
 
        #region Private Methods

        private void ApplyItemContainerStyle(DependencyObject container, object item)
        { 
            FrameworkObject foContainer = new FrameworkObject(container);
 
            // don't overwrite a locally-defined style (bug 1018408) 
            if (!foContainer.IsStyleSetFromGenerator &&
                container.ReadLocalValue(FrameworkElement.StyleProperty) != DependencyProperty.UnsetValue) 
            {
                return;
            }
 
            // Control's ItemContainerStyle has first stab
            Style style = ItemContainerStyle; 
 
            // no ItemContainerStyle set, try ItemContainerStyleSelector
            if (style == null) 
            {
                if (ItemContainerStyleSelector != null)
                {
                    style = ItemContainerStyleSelector.SelectStyle(item, container); 
                }
            } 
 
            // apply the style, if found
            if (style != null) 
            {
                // verify style is appropriate before applying it
                if (!style.TargetType.IsInstanceOfType(container))
                    throw new InvalidOperationException(SR.Get(SRID.StyleForWrongType, style.TargetType.Name, container.GetType().Name)); 

                foContainer.Style = style; 
                foContainer.IsStyleSetFromGenerator = true; 
            }
            else if (foContainer.IsStyleSetFromGenerator) 
            {
                // if Style was formerly set from ItemContainerStyle, clear it
                foContainer.IsStyleSetFromGenerator = false;
                container.ClearValue(FrameworkElement.StyleProperty); 
            }
        } 
 
        private void RemoveItemContainerStyle(DependencyObject container)
        { 
            FrameworkObject foContainer = new FrameworkObject(container);

            if (foContainer.IsStyleSetFromGenerator)
            { 
                container.ClearValue(FrameworkElement.StyleProperty);
            } 
        } 

 
        internal object GetItemOrContainerFromContainer(DependencyObject container)
        {
            object item = ItemContainerGenerator.ItemFromContainer(container);
 
            if (item == DependencyProperty.UnsetValue
                && ItemsControlFromItemContainer(container) == this 
                && ((IGeneratorHost)this).IsItemItsOwnContainer(container)) 
            {
                item = container; 
            }

            return item;
        } 

        #endregion 
 
        #region ItemValueStorage
 

        internal object ReadItemValue(object item, int dpIndex)
        {
            if (item != null) 
            {
                List<KeyValuePair<int, object>> itemValues = GetItemValues(item); 
 
                if (itemValues != null)
                { 
                    for (int i = 0; i < itemValues.Count; i++)
                    {
                        if (itemValues[i].Key == dpIndex)
                        { 
                            return itemValues[i].Value;
                        } 
                    } 
                }
            } 

            return null;
        }
 
        /// <summary>
        /// Stores the given value in ItemValueStorage, associating it with the given item and DependencyProperty index. 
        /// </summary> 
        /// <param name="item"></param>
        /// <param name="value"></param> 
        /// <param name="index">global index of a DependencyProperty</param>
        internal void StoreItemValue(object item, object value, int dpIndex)
        {
            if (item != null) 
            {
                List<KeyValuePair<int, object>> itemValues = EnsureItemValues(item); 
 
                //
                // Find the key, if it exists, and modify its value.  Since the number of DPs we want to store 
                // is typically very small, using a List in this manner is faster than hashing
                //

                bool found = false; 
                KeyValuePair<int, object> keyValue = new KeyValuePair<int, object>(dpIndex, value);
 
                for (int j = 0; j < itemValues.Count; j++) 
                {
                    if (itemValues[j].Key == dpIndex) 
                    {
                        itemValues[j] = keyValue;
                        found = true;
                        break; 
                    }
                } 
 
                if (!found)
                { 
                    itemValues.Add(keyValue);
                }
            }
        } 

 
        /// <summary> 
        /// Returns the ItemValues list for a given item.  May return null if one hasn't been set yet.
        /// </summary> 
        /// <param name="item"></param>
        /// <returns></returns>
        private List<KeyValuePair<int, object>> GetItemValues(object item)
        { 
            return GetItemValues(item, ItemValueStorageField.GetValue(this));
        } 
 
        private List<KeyValuePair<int, object>> GetItemValues(object item,
                                                              Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>> itemValueStorage) 
        {
            Debug.Assert(item != null);
            List<KeyValuePair<int, object>> itemValues = null;
            WeakReferenceKey<object> key; 

            if (itemValueStorage != null) 
            { 
                key = new WeakReferenceKey<object>(item);
                itemValueStorage.TryGetValue(key, out itemValues); 
            }

            return itemValues;
        } 

 
        private List<KeyValuePair<int, object>> EnsureItemValues(object item) 
        {
            WeakReferenceKey<object> key; 

            Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>> itemValueStorage = EnsureItemValueStorage();
            List<KeyValuePair<int, object>> itemValues = GetItemValues(item, itemValueStorage);
 
            if (itemValues == null && HashHelper.HasReliableHashCode(item))
            { 
                key = new WeakReferenceKey<object>(item); 
                itemValues = new List<KeyValuePair<int, object>>(3);    // So far the only use of this is to store three values.
                itemValueStorage[key] = itemValues; 
            }

            return itemValues;
        } 

 
        private Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>> EnsureItemValueStorage() 
        {
            Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>> itemValueStorage = ItemValueStorageField.GetValue(this); 

            if (itemValueStorage == null)
            {
                itemValueStorage = new Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>>(Items.Count); 
                ItemValueStorageField.SetValue(this, itemValueStorage);
            } 
 
            return itemValueStorage;
        } 

        /// <summary>
        /// Sets all values saved in ItemValueStorage for the given item onto the container
        /// </summary> 
        /// <param name="container"></param>
        /// <param name="item"></param> 
        private void SetItemValuesOnContainer(DependencyObject container, object item, int[] dpIndices) 
        {
            List<KeyValuePair<int, object>> itemValues = GetItemValues(item); 

            if (itemValues != null)
            {
                for (int i = 0; i < itemValues.Count; i++) 
                {
                    int dpIndex = itemValues[i].Key; 
 
                    for (int j = 0; j < dpIndices.Length; j++)
                    { 
                        if (dpIndex == dpIndices[j])
                        {
                            object value = itemValues[i].Value;
                            EntryIndex entryIndex = container.LookupEntry(dpIndex); 
                            ModifiedItemValue modifiedItemValue = value as ModifiedItemValue;
                            DependencyProperty dp = DependencyProperty.RegisteredPropertyList.List[dpIndex]; 
 
                            if (modifiedItemValue == null)
                            { 
                                // set as local value
                                if (dp != null)
                                {
                                    // for real properties, call SetValue so that the property's 
                                    // change-callback is called
                                    container.SetValue(dp, value); 
                                } 
                                else
                                { 
                                    // for "fake" properties (no corresponding DP - e.g. VSP's desired-size),
                                    // set the property directly into the effective value table
                                    container.SetEffectiveValue(entryIndex, null /*dp*/, dpIndex, null /*metadata*/, value, BaseValueSourceInternal.Local);
                                } 
                            }
                            else if (modifiedItemValue.IsCoercedWithCurrentValue) 
                            { 
                                // set as current-value
                                container.SetCurrentValue(dp, modifiedItemValue.Value); 
                            }
                            break;
                        }
                    } 
                }
            } 
        } 

        /// <summary> 
        /// Stores the value of a container for the given item and set of dependency properties
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param> 
        /// <param name="dpIndices"></param>
        private void StoreItemValues(DependencyObject container, object item, int[] dpIndices) 
        { 

            // 
            // Loop through all DPs we care about storing.  If the container has a current-value or locally-set value we'll store it.
            //
            for (int i = 0; i < dpIndices.Length; i++)
            { 
                int dpIndex = dpIndices[i];
                EntryIndex entryIndex = container.LookupEntry(dpIndex); 
 
                if (entryIndex.Found)
                { 
                    EffectiveValueEntry entry = container.EffectiveValues[entryIndex.Index];

                    if (entry.BaseValueSourceInternal == BaseValueSourceInternal.Local && !entry.HasModifiers)
                    { 
                        // store local values that aren't modified
                        StoreItemValue(item, entry.Value, dpIndex); 
                    } 
                    else if (entry.IsCoercedWithCurrentValue)
                    { 
                        // store current-values
                        StoreItemValue(item,
                                        new ModifiedItemValue(entry.ModifiedValue.CoercedValue, FullValueSource.IsCoercedWithCurrentValue),
                                        dpIndex); 
                    }
                } 
 
            }
        } 

        // This class reprents an item value that arises from a non-local source (e.g. current-value)
        private class ModifiedItemValue
        { 
            public ModifiedItemValue(object value, FullValueSource valueSource)
            { 
                _value = value; 
                _valueSource = valueSource;
            } 

            public object Value { get { return _value; } }

            public bool IsCoercedWithCurrentValue 
            {
                get { return (_valueSource & FullValueSource.IsCoercedWithCurrentValue) != 0; } 
            } 

            object _value; 
            FullValueSource _valueSource;
        }

 
        #endregion
 
        #region Method Overrides 

        /// <summary> 
        ///     Returns a string representation of this object.
        /// </summary>
        /// <returns></returns>
        public override string ToString() 
        {
            // HasItems may be wrong when underlying collection does not notify, 
            // but this function should try to return what's consistent with ItemsControl state. 
            int itemsCount = HasItems ? Items.Count : 0;
            return SR.Get(SRID.ToStringFormatString_ItemsControl, this.GetType(), itemsCount); 
        }

        #endregion
 
        #region Data
 
        private ItemCollection _items;                      // Cache for Items property 
        private ItemContainerGenerator _itemContainerGenerator;
        private Panel _itemsHost; 
        private ScrollViewer _scrollHost;
        private ObservableCollection<GroupStyle> _groupStyle = new ObservableCollection<GroupStyle>();

 
        // ItemValueStorage.  For each data item it stores a list of (DP, value) pairs that we want to preserve on the container.
        private static readonly UncommonField<Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>>> ItemValueStorageField = 
                            new UncommonField<Dictionary<WeakReferenceKey<object>, List<KeyValuePair<int, object>>>>(); 

        // Since ItemValueStorage is private and only used for TreeView virtualization we hardcode the DPs that we'll store in it. 
        // If we make this available as a service to the rest of the platform we'd come up with some sort of DP registration mechanism.
        private static readonly int[] ItemValueStorageIndices = new int[] { ItemValueStorageField.GlobalIndex, TreeViewItem.IsExpandedProperty.GlobalIndex };

 
        #endregion
 
        #region DTypeThemeStyleKey 

        // Returns the DependencyObjectType for the registered ThemeStyleKey's default 
        // value. Controls will override this method to return approriate types.
        internal override DependencyObjectType DTypeThemeStyleKey
        {
            get { return _dType; } 
        }
 
        private static DependencyObjectType _dType; 

        #endregion DTypeThemeStyleKey 
    }
}


