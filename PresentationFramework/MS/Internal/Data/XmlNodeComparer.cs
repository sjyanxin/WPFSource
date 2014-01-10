//---------------------------------------------------------------------------- 
//
// <copyright file="XmlNodeComparer.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Defines XmlNodeComparer object, used to sort a view of data produced by an XmlDataSource. 
// 
// Specs:       http://avalon/connecteddata/M5%20Specs/UIBinding.mht
// 
//---------------------------------------------------------------------------

using System;
using System.Collections; 
using System.Collections.Generic;
using System.ComponentModel; 
using System.Globalization; 

using System.Xml; 
using MS.Internal.Data;

namespace MS.Internal.Data
{ 
    /// <summary>
    /// The XmlNodeComparer is used to sort a view of data produced by an XmlDataSource. 
    /// </summary> 
    internal class XmlNodeComparer : IComparer
    { 
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sortParameters"> array of sort parameters </param> 
        /// <param name="namespaceManager"> namespace manager, to control queries</param>
        /// <param name="culture">culture to use for comparisons</param> 
        internal XmlNodeComparer(SortDescriptionCollection sortParameters, XmlNamespaceManager namespaceManager, CultureInfo culture) 
        {
            _sortParameters = sortParameters; 
            _namespaceManager = namespaceManager;
            _culture = (culture == null) ? CultureInfo.InvariantCulture : culture;
        }
 
        int IComparer.Compare(object o1, object o2)
        { 
            int result = 0; 
            XmlNode node1 = o1 as XmlNode;
            XmlNode node2 = o2 as XmlNode; 

            if (node1 == null)
                return -1;
            if (node2 == null) 
                return +1;
 
            for (int k = 0; k < _sortParameters.Count; ++k) 
            {
                string valueX = AssemblyHelper.SelectStringValue(node1, _sortParameters[k].PropertyName, _namespaceManager); 
                string valueY = AssemblyHelper.SelectStringValue(node2, _sortParameters[k].PropertyName, _namespaceManager);

                result = String.Compare(valueX, valueY, false, _culture);
                if (_sortParameters[k].Direction == ListSortDirection.Descending) 
                    result = -result;
 
                if (result != 0) 
                    break;
            } 

            return result;
        }
 
        private SortDescriptionCollection  _sortParameters;
        private XmlNamespaceManager  _namespaceManager; 
        CultureInfo _culture; 
    }
} 



