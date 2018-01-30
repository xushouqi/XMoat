using System;

namespace XMoat.Hotfix
{
    public static class Init
    {
        public static void Start()
        {
            try
            {
                //Hotfix.Scene.ModelScene = Game.Scene;
                //Hotfix.Scene.AddComponent<UIComponent>();
            }
            catch (Exception e)
            {
                //Log.Error(e.ToStr());
            }
        }

    }
}
