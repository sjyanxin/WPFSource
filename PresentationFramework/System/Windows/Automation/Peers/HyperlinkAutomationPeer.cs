//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
// File: HyperlinkAutomationPeer.cs 
//
// Description: Automation peer for hyperlink 
// 
//---------------------------------------------------------------------------
 
using System.Windows.Automation.Provider;   // IRawElementProviderSimple
using System.Windows.Documents;

namespace System.Windows.Automation.Peers 
{
    /// 
    public class HyperlinkAutomationPeer : TextElementAutomationPeer, IInvokeProvider 
    {
        /// 
        public HyperlinkAutomationPeer(Hyperlink owner)
            : base(owner)
        { }
 
        /// <summary>
        /// 
        /// </summary> 
        /// <param name="patternInterface"></param>
        /// <returns></returns> 
        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Invoke)
            { 
                return this;
            } 
            else 
            {
                return base.GetPattern(patternInterface); 
            }
        }

        //Default Automation properties 
        ///
        protected override AutomationControlType GetAutomationControlTypeCore() 
        { 
            return AutomationControlType.Hyperlink;
        } 

        /// <summary>
        ///
        /// </summary> 
        protected override string GetNameCore()
        { 
            string name = base.GetNameCore(); 

            if (name == string.Empty) 
            {
                Hyperlink owner = (Hyperlink)Owner;

                name = owner.Text; 

                if (name == null) 
                    name = string.Empty; 
            }
 
            return name;
        }

        /// 
        override protected string GetClassNameCore()
        { 
            return "Hyperlink"; 
        }
 
        /// <summary>
        /// <see cref="AutomationPeer.IsControlElementCore"/>
        /// </summary>
        override protected bool IsControlElementCore() 
        {
            return true; 
        } 

        //Invoke Pattern implementation 
        void IInvokeProvider.Invoke()
        {
            if (!IsEnabled())
                throw new ElementNotEnabledException(); 

            Hyperlink owner = (Hyperlink)Owner; 
            owner.DoClick(); 
        }
    } 
}

