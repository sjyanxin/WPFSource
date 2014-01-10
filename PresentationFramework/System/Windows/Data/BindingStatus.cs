using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Windows.Data
{
    public enum BindingStatus
    {
        Unattached,
        Inactive,
        Active,
        Detached,
        AsyncRequestPending,
        PathError,
        UpdateTargetError,
        UpdateSourceError
    }

 

}
