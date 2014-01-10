using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Windows
{
    // 摘要:
    //     表示在依赖项对象的有效属性值更改时调用的回调。
    //
    // 参数:
    //   d:
    //     属性值已更改的 System.Windows.DependencyObject。
    //
    //   e:
    //     由任何事件发出的事件数据，该事件跟踪对此属性的有效值的更改。
    public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);
}
