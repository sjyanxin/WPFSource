//---------------------------------------------------------------------------- 
//
// <copyright file="CollectionViewGroupRoot.cs" company="Microsoft">
//    Copyright (C) 2003 by Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// 
// Description: Root of CollectionViewGroup structure, as created by a CollectionView according to a GroupDescription. 
//                  CollectionView classes use this class to manage all Grouping functionality.
// 
// See spec at http://avalon/connecteddata/Specs/Grouping.mht
//
//---------------------------------------------------------------------------
 
using System;
using System.Collections;       // IComparer 
using System.Collections.ObjectModel;   // ObservableCollection 
using System.Collections.Specialized;   // INotifyCollectionChanged
using System.ComponentModel;    // PropertyChangedEventArgs, GroupDescription 
using System.Diagnostics;       // Debug.Assert
using System.Globalization;
using System.Windows.Data;      // CollectionViewGroup
 
namespace MS.Internal.Data
{ 
 
    // CollectionView classes use this class as the manager of all Grouping functionality
    internal class CollectionViewGroupRoot : CollectionViewGroupInternal, INotifyCollectionChanged 
    {
        internal CollectionViewGroupRoot(CollectionView view) : base("Root", null)
        {
            _view = view; 
        }
 
#region INotifyCollectionChanged 
        /// <summary>
        /// Raise this event when the (grouped) view changes 
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary> 
        ///     Notify listeners that this View has changed
        /// </summary> 
        /// <remarks> 
        ///     CollectionViews (and sub-classes) should take their filter/sort/grouping
        ///     into account before calling this method to forward CollectionChanged events. 
        /// </remarks>
        /// <param name="args">
        ///     The NotifyCollectionChangedEventArgs to be passed to the EventHandler
        /// </param> 
        public void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        { 
            if (args == null) 
                throw new ArgumentNullException("args");
 
            if (CollectionChanged != null)
                CollectionChanged(this, args);
        }
#endregion INotifyCollectionChanged 

        /// <summary> 
        /// The description of grouping, indexed by level. 
        /// </summary>
        public virtual ObservableCollection<GroupDescription> GroupDescriptions 
        {
            get { return _groupBy; }
        }
 
        /// <summary>
        /// A delegate to select the group description as a function of the 
        /// parent group and its level. 
        /// </summary>
        public virtual GroupDescriptionSelectorCallback GroupBySelector 
        {
            get { return _groupBySelector; }
            set { _groupBySelector = value; }
        } 

        // a group description has changed somewhere in the tree - notify host 
        protected override void OnGroupByChanged() 
        {
            if (GroupDescriptionChanged != null) 
                GroupDescriptionChanged(this, EventArgs.Empty);
        }

#region Internal Events and Properties 

        internal event EventHandler GroupDescriptionChanged; 
 
        internal IComparer ActiveComparer
        { 
            get { return _comparer; }
            set { _comparer = value; }
        }
 
        /// <summary>
        /// Culture to use during sorting. 
        /// </summary> 
        internal CultureInfo Culture
        { 
            get { return _view.Culture; }
        }

        internal bool IsDataInGroupOrder 
        {
            get { return _isDataInGroupOrder; } 
            set { _isDataInGroupOrder = value; } 
        }
 
#endregion Internal Events and Properties

#region Internal Methods
 
        internal void Initialize()
        { 
            if (_topLevelGroupDescription == null) 
            {
                _topLevelGroupDescription = new TopLevelGroupDescription(); 
            }
            InitializeGroup(this, _topLevelGroupDescription, 0);
        }
 
        internal void AddToSubgroups(object item, bool loading)
        { 
            AddToSubgroups(item, this, 0, loading); 
        }
 
        internal bool RemoveFromSubgroups(object item)
        {
            return RemoveFromSubgroups(item, this, 0);
        } 

        internal void RemoveItemFromSubgroupsByExhaustiveSearch(object item) 
        { 
            RemoveItemFromSubgroupsByExhaustiveSearch(this, item);
        } 

        internal void InsertSpecialItem(int index, object item, bool loading)
        {
            ChangeCounts(item, +1); 
            ProtectedItems.Insert(index, item);
 
            if (!loading) 
            {
                int globalIndex = this.LeafIndexFromItem(item, index); 
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, globalIndex));
            }
        }
 
        internal void RemoveSpecialItem(int index, object item, bool loading)
        { 
            Debug.Assert(Object.Equals(item, ProtectedItems[index]), "RemoveSpecialItem finds inconsistent data"); 
            int globalIndex = -1;
 
            if (!loading)
            {
                globalIndex = this.LeafIndexFromItem(item, index);
            } 

            ChangeCounts(item, -1); 
            ProtectedItems.RemoveAt(index); 

            if (!loading) 
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, globalIndex));
            }
        } 

        protected override int FindIndex(object item, object seed, IComparer comparer, int low, int high) 
        { 
            // root group needs to adjust the bounds of the search to exclude the
            // placeholder and new item (if any) 
            IEditableCollectionView iecv = _view as IEditableCollectionView;
            if (iecv != null)
            {
                if (iecv.NewItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning) 
                {
                    ++low; 
                    if (iecv.IsAddingNew) 
                    {
                        ++low; 
                    }
                }
                else
                { 
                    if (iecv.IsAddingNew)
                    { 
                        --high; 
                    }
                    if (iecv.NewItemPlaceholderPosition == NewItemPlaceholderPosition.AtEnd) 
                    {
                        --high;
                    }
                } 
            }
 
            return base.FindIndex(item, seed, comparer, low, high); 
        }
 

#endregion Internal Methods

#region private methods 

        // Initialize the given group 
        void InitializeGroup(CollectionViewGroupInternal group, GroupDescription parentDescription, int level) 
        {
            // set the group description for dividing the group into subgroups 
            GroupDescription groupDescription = GetGroupDescription(group, parentDescription, level);
            group.GroupBy = groupDescription;

            // create subgroups for each of the explicit names 
            ObservableCollection<object> explicitNames =
                        (groupDescription != null) ? groupDescription.GroupNames : null; 
            if (explicitNames != null) 
            {
                for (int k=0, n=explicitNames.Count;  k<n;  ++k) 
                {
                    CollectionViewGroupInternal subgroup = new CollectionViewGroupInternal(explicitNames[k], group);
                    InitializeGroup(subgroup, groupDescription, level+1);
                    group.Add(subgroup); 
                }
            } 
 
            group.LastIndex = 0;
        } 


        // return the description of how to divide the given group into subgroups
        GroupDescription GetGroupDescription(CollectionViewGroup group, GroupDescription parentDescription, int level) 
        {
            GroupDescription result = null; 
            if (group == this) 
            {
                group = null;       // users don't see the synthetic group 
            }

            if (parentDescription != null)
            { 
#if GROUPDESCRIPTION_HAS_SUBGROUP
                // a. Use the parent description's subgroup description 
                result = parentDescription.Subgroup; 
#endif // GROUPDESCRIPTION_HAS_SUBGROUP
 
#if GROUPDESCRIPTION_HAS_SELECTOR
                // b. Call the parent description's selector
                if (result == null && parentDescription.SubgroupSelector != null)
                { 
                    result = parentDescription.SubgroupSelector(group, level);
                } 
#endif // GROUPDESCRIPTION_HAS_SELECTOR 
            }
 
            // c. Call the global chooser
            if (result == null && GroupBySelector != null)
            {
                result = GroupBySelector(group, level); 
            }
 
            // d. Use the global array 
            if (result == null && level < GroupDescriptions.Count)
            { 
                result = GroupDescriptions[level];
            }

            return result; 
        }
 
        // add an item to the desired subgroup(s) of the given group 
        void AddToSubgroups(object item, CollectionViewGroupInternal group, int level, bool loading)
        { 
            object name = GetGroupName(item, group.GroupBy, level);
            ICollection nameList;

            if (name == UseAsItemDirectly) 
            {
                // the item belongs to the group itself (not to any subgroups) 
                if (loading) 
                {
                    group.Add(item); 
                }
                else
                {
                    int localIndex = group.Insert(item, item, ActiveComparer); 
                    int index = group.LeafIndexFromItem(item, localIndex);
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index)); 
                } 
            }
            else if ((nameList = name as ICollection) == null) 
            {
                // the item belongs to one subgroup
                AddToSubgroup(item, group, level, name, loading);
            } 
            else
            { 
                // the item belongs to multiple subgroups 
                foreach (object o in nameList)
                { 
                    AddToSubgroup(item, group, level, o, loading);
                }
            }
        } 

 
        // add an item to the subgroup with the given name 
        void AddToSubgroup(object item, CollectionViewGroupInternal group, int level, object name, bool loading)
        { 
            CollectionViewGroupInternal subgroup;
            int index = (loading && IsDataInGroupOrder) ? group.LastIndex : 0;

            // find the desired subgroup 
            for (int n=group.Items.Count;  index < n;  ++index)
            { 
                subgroup = group.Items[index] as CollectionViewGroupInternal; 
                if (subgroup == null)
                    continue;           // skip children that are not groups 

                if (group.GroupBy.NamesMatch(subgroup.Name, name))
                {
                    group.LastIndex = index; 
                    AddToSubgroups(item, subgroup, level+1, loading);
                    return; 
                } 
            }
 
            // the item didn't match any subgroups.  Create a new subgroup and add the item.
            subgroup = new CollectionViewGroupInternal(name, group);
            InitializeGroup(subgroup, group.GroupBy, level+1);
 
            if (loading)
            { 
                group.Add(subgroup); 
                group.LastIndex = index;
            } 
            else
            {
                group.Insert(subgroup, item, ActiveComparer);
            } 

            AddToSubgroups(item, subgroup, level+1, loading); 
        } 

        // remove an item from the desired subgroup(s) of the given group. 
        // Return true if the item was not in one of the subgroups it was supposed to be.
        bool RemoveFromSubgroups(object item, CollectionViewGroupInternal group, int level)
        {
            bool itemIsMissing = false; 
            object name = GetGroupName(item, group.GroupBy, level);
            ICollection nameList; 
 
            if (name == UseAsItemDirectly)
            { 
                // the item belongs to the group itself (not to any subgroups)
                itemIsMissing = RemoveFromGroupDirectly(group, item);
            }
            else if ((nameList = name as ICollection) == null) 
            {
                // the item belongs to one subgroup 
                if (RemoveFromSubgroup(item, group, level, name)) 
                    itemIsMissing = true;
            } 
            else
            {
                // the item belongs to multiple subgroups
                foreach (object o in nameList) 
                {
                    if (RemoveFromSubgroup(item, group, level, o)) 
                        itemIsMissing = true; 
                }
            } 

            return itemIsMissing;
        }
 

        // remove an item from the subgroup with the given name. 
        // Return true if the item was not in one of the subgroups it was supposed to be. 
        bool RemoveFromSubgroup(object item, CollectionViewGroupInternal group, int level, object name)
        { 
            bool itemIsMissing = false;
            CollectionViewGroupInternal subgroup;

            // find the desired subgroup 
            for (int index=0, n=group.Items.Count;  index < n;  ++index)
            { 
                subgroup = group.Items[index] as CollectionViewGroupInternal; 
                if (subgroup == null)
                    continue;           // skip children that are not groups 

                if (group.GroupBy.NamesMatch(subgroup.Name, name))
                {
                    if (RemoveFromSubgroups(item, subgroup, level+1)) 
                        itemIsMissing = true;
                    return itemIsMissing; 
                } 
            }
 
            // the item didn't match any subgroups.  It should have.
            return true;
        }
 

        // remove an item from the direct children of a group. 
        // Return true if this couldn't be done. 
        bool RemoveFromGroupDirectly(CollectionViewGroupInternal group, object item)
        { 
            int leafIndex = group.Remove(item, true);
            if (leafIndex >= 0)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, leafIndex)); 
                return false;
            } 
            else 
            {
                return true; 
            }
        }

        // the item did not appear in one or more of the subgroups it 
        // was supposed to.  This can happen if the item's properties
        // change so that the group names we used to insert it are 
        // different from the names used to remove it.  If this happens, 
        // remove the item the hard way.
        void RemoveItemFromSubgroupsByExhaustiveSearch(CollectionViewGroupInternal group, object item) 
        {
            // try to remove the item from the direct children
            if (RemoveFromGroupDirectly(group, item))
            { 
                // if that didn't work, recurse into each subgroup
                // (loop runs backwards in case an entire group is deleted) 
                for (int k=group.Items.Count-1;  k >= 0;  --k) 
                {
                    CollectionViewGroupInternal subgroup = group.Items[k] as CollectionViewGroupInternal; 
                    if (subgroup != null)
                    {
                        RemoveItemFromSubgroupsByExhaustiveSearch(subgroup, item);
                    } 
                }
            } 
            else 
            {
                // if the item was removed directly, we don't have to look at subgroups. 
                // An item cannot appear both as a direct child and as a deeper descendant.
            }
        }
 

        // get the group name(s) for the given item 
        object GetGroupName(object item, GroupDescription groupDescription, int level) 
        {
            if (groupDescription != null) 
            {
                return groupDescription.GroupNameFromItem(item, level, Culture);
            }
            else 
            {
                return UseAsItemDirectly; 
            } 
        }
#endregion private methods 

#region private fields
        CollectionView _view;
        IComparer _comparer; 
        bool _isDataInGroupOrder = false;
 
        ObservableCollection<GroupDescription> _groupBy = new ObservableCollection<GroupDescription>(); 
        GroupDescriptionSelectorCallback _groupBySelector;
        static GroupDescription _topLevelGroupDescription; 
        static readonly object UseAsItemDirectly = new NamedObject("UseAsItemDirectly");
#endregion private fields

#region private types 
        private class TopLevelGroupDescription : GroupDescription
        { 
            public TopLevelGroupDescription() 
            {
            } 

            // we have to implement this abstract method, but it should never be called
            public override object GroupNameFromItem(object item, int level, System.Globalization.CultureInfo culture)
            { 
                throw new NotSupportedException();
            } 
        } 
#endregion private types
    } 

}


