using System;
using System.Collections.Generic;
using System.Text;

namespace XMoat.Common
{
    public class CommonManager
    {
        private static CommonManager instance;
        public static CommonManager Instance
        {
            get
            {
                return instance ?? (instance = new CommonManager());
            }
        }

        private ObjectPool objPool;
        public ObjectPool ObjectPool
        {
            get
            {
                return objPool ?? (objPool = new ObjectPool());
            }
        }
    }
}
