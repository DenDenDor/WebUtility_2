using System.Collections.Generic;
using UnityEngine;

namespace WebUtility
{
    public interface IDIRouter 
    {
        List<IPresenter> Init();
    }
}
