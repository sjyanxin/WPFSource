using System; 
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security; 
using System.Text;
using System.Windows; 
using System.Windows.Automation.Provider; 
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
 
using MS.Internal;
using MS.Win32; 
 
namespace System.Windows.Automation.Peers
{ 
    ///
    public class GroupItemAutomationPeer : FrameworkElementAutomationPeer
    {
        /// 
        public GroupItemAutomationPeer(GroupItem owner): base(owner)
        { 
        } 

        /// 
        override protected string GetClassNameCore()
        {
            return "GroupItem";
        } 

        /// 
        override protected AutomationControlType GetAutomationControlTypeCore() 
        {
            return AutomationControlType.Group; 
        }

        ///
        override protected bool IsOffscreenCore() 
        {
            if (!Owner.IsVisible) 
                return true; 

            Rect boundingRect = CalculateVisibleBoundingRect(); 
            return (boundingRect == Rect.Empty || boundingRect.Height == 0 || boundingRect.Width == 0);
        }

        /// 
        protected override List<AutomationPeer> GetChildrenCore()
        { 
            GroupItem owner = (GroupItem)Owner; 
            ItemsControl itemsControl = ItemsControl.ItemsControlFromItemContainer(Owner);
            if (itemsControl != null) 
            {
                ItemsControlAutomationPeer itemsControlAP = itemsControl.CreateAutomationPeer() as ItemsControlAutomationPeer;
                if (itemsControlAP != null)
                { 
                    ItemContainerGenerator generator = owner.Generator;
                    if (generator != null) 
                    { 
                        IList items = generator.Items;
                        List<AutomationPeer> children = new List<AutomationPeer>(items.Count); 
                        ItemPeersStorage<ItemAutomationPeer> oldChildren = _dataChildren; //cache the old ones for possible reuse
                        _dataChildren = new ItemPeersStorage<ItemAutomationPeer>();
                        if (items.Count > 0)
                        { 
                            foreach (object item in items)
                            { 
                                CollectionViewGroup cvg = item as CollectionViewGroup; 
                                if (cvg != null)
                                { 
                                    GroupItem groupItem = generator.ContainerFromItem(item) as GroupItem;
                                    if (groupItem != null)
                                    {
                                        GroupItemAutomationPeer peer = groupItem.CreateAutomationPeer() as GroupItemAutomationPeer; 
                                        if (peer != null)
                                            children.Add(peer); 
                                    } 
                                }
                                else 
                                {
                                    //try to reuse old peer if it exists
                                    ItemAutomationPeer peer = oldChildren[item];
                                    //no old peer - create new one 
                                    if (peer == null)
                                        peer = itemsControlAP.CreateItemAutomationPeerInternal(item); 
 
                                    //perform hookup so the events sourced from wrapper peer are fired as if from the data item
                                    if (peer != null) 
                                    {
                                        AutomationPeer wrapperPeer = peer.GetWrapperPeer();
                                        if (wrapperPeer != null)
                                        { 
                                            wrapperPeer.EventsSource = peer;
                                            if (peer.ChildrenValid && peer.Children == null && this.AncestorsInvalid) 
                                            { 
                                                peer.AncestorsInvalid = true;
                                                wrapperPeer.AncestorsInvalid = true; 
                                            }
                                        }
                                    }
 
                                    //protection from indistinguishable items - for example, 2 strings with same value
                                    //this scenario does not work in ItemsControl however is not checked for. 
                                    if (_dataChildren[item] == null) 
                                    {
                                        children.Add(peer); 
                                        _dataChildren[item] = peer;

                                        // Update ItemsControl cache too
                                        // ItemPeers needs to be updated because used in Selector and other ItemsControl subclasses to access item peers directly 
                                        if (itemsControlAP.ItemPeers[item] == null)
                                        { 
                                            itemsControlAP.ItemPeers[item] = peer; 
                                        }
                                    } 
                                }
                            }

                            return children; 
                        }
                    } 
                } 
            }
 
            return null;
        }

        private ItemPeersStorage<ItemAutomationPeer> _dataChildren = new ItemPeersStorage<ItemAutomationPeer>(); 

    } 
} 


