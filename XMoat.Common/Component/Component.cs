using System;
using System.Collections.Generic;
using System.Text;

namespace XMoat.Common
{
    public class Component : Disposer
    {

        protected Component()
        {
            //this.Id = 1;
        }

        public override void Dispose()
        {
            if (this.Id == 0)
            {
                return;
            }

            base.Dispose();
        }
    }
}
