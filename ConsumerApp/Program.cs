using System;
using System.Runtime.InteropServices;

namespace ConsumerApp
{
    class Program
    {
        static void Main()
        {
            if (InstallGetActivationFactoryHook())
            {
                Console.ReadLine();

                TryUseWinRtComponent();

                RemoveGetActivationFactoryHook();
            }

            TryUseWinRtComponent();

            Console.ReadLine();
        }

        static void TryUseWinRtComponent()
        {
            var myClass = new WinRtComponent.NativeClass();
            var greeting = myClass.GreetUser("Ninja Cat");
            Console.WriteLine(greeting);
        }

        [DllImport("HooksManager.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern bool InstallGetActivationFactoryHook();

        [DllImport("HooksManager.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern bool RemoveGetActivationFactoryHook();
    }
}
