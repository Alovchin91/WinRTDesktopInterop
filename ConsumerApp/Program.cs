using System;
using System.Runtime.InteropServices;

namespace ConsumerApp
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("> Press ENTER to install hooks and use WinRT component.");
            Console.ReadLine();

            if (InstallRoFunctionHooks())
            {
                UseWinRtComponent_DefaultCtor();
                UseWinRtComponent_Factory();
                UseWinRtComponent_StaticMethod();
                UseWinRtComponent_Throwing();

                Console.WriteLine(Environment.NewLine + "> Now press ENTER to remove hooks and finish.");
                Console.ReadLine();

                RemoveRoFunctionHooks();
            }
            else
            {
                Console.WriteLine("Failed to install hooks. Press ENTER to finish.");
                Console.ReadLine();
            }
        }

        static void UseWinRtComponent_DefaultCtor()
        {
            var myClass = new WinRtComponent.NativeClass();
            var greeting = myClass.GreetUser("Ninja Cat");
            Console.WriteLine(greeting);
        }

        static void UseWinRtComponent_Factory()
        {
            var myClass = new WinRtComponent.NativeClass("Greetings to %s from factory!");
            var greeting = myClass.GreetUser("Ninja Cat");
            Console.WriteLine(greeting);
        }

        static void UseWinRtComponent_StaticMethod()
        {
            var myClass = new WinRtComponent.NativeClass();
            WinRtComponent.NativeClass.SetGreeting(myClass, "Cheers to %s from statics!");
            var greeting = myClass.GreetUser("Ninja Cat");
            Console.WriteLine(greeting);
        }

        static void UseWinRtComponent_Throwing()
        {
            var myClass = new WinRtComponent.NativeClass();
            try
            {
                myClass.GreetUser(string.Empty); // throws ArgumentException
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine("Greeting format was: " + myClass.Greeting);
            }
        }

        [DllImport("HooksManager.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern bool InstallRoFunctionHooks();

        [DllImport("HooksManager.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern bool RemoveRoFunctionHooks();
    }
}
