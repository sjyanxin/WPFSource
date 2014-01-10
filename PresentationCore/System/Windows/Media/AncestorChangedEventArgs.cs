using System; 

namespace System.Windows.Media
{
    internal class AncestorChangedEventArgs 
    {
        public AncestorChangedEventArgs(DependencyObject subRoot, DependencyObject oldParent) 
        { 
            _subRoot = subRoot;
            _oldParent = oldParent; 
        }

        public DependencyObject Ancestor
        { 
            get { return _subRoot; }
        } 
 
        public DependencyObject OldParent
        { 
            get { return _oldParent; }
        }

        private DependencyObject _subRoot;     // direct child of the changed link. 
        private DependencyObject _oldParent;   // If link is cut; otherwise null.
    } 
} 

