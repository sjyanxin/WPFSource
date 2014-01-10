// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
// <OWNER>[....]</OWNER>
// 
 
//
// GenericPrincipal.cs 
//

namespace System.Security.Principal
{ 
    using System.Runtime.Remoting;
    using System; 
    using System.Security.Util; 
    using System.Globalization;
    using System.Diagnostics.Contracts; 

    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)]
    public class GenericPrincipal : IPrincipal { 
        private IIdentity m_identity;
        private string[] m_roles; 
 
        public GenericPrincipal (IIdentity identity, string[] roles) {
            if (identity == null) 
                throw new ArgumentNullException("identity");
            Contract.EndContractBlock();

            m_identity = identity; 
            if (roles != null) {
                m_roles = new string[roles.Length]; 
                for (int i = 0; i < roles.Length; ++i) { 
                    m_roles[i] = roles[i];
                } 
            }
            else {
                m_roles = null;
            } 
        }
 
        public virtual IIdentity Identity { 
            get {
                return m_identity; 
            }
        }

        public virtual bool IsInRole (string role) { 
            if (role == null || m_roles == null)
                return false; 
 
            for (int i = 0; i < m_roles.Length; ++i) {
                if (m_roles[i] != null && String.Compare(m_roles[i], role, StringComparison.OrdinalIgnoreCase) == 0) 
                    return true;
            }
            return false;
        } 
    }
} 

