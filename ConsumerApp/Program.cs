using System;
//using System.Runtime.CompilerServices;

namespace ConsumerApp
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("> Press ENTER to install hooks and use WinRT component.");
            Console.ReadLine();

            var managedHooksManager = new ManagedHooksManager.HooksManager();
            managedHooksManager.SetupHooks();
            managedHooksManager.RegisterWinRtType("WinRtComponent.NativeClass, WinRtComponent, ContentType=WindowsRuntime");
            //RegisterWinRtTypes(managedHooksManager);

            UseWinRtComponent_DefaultCtor();
            UseWinRtComponent_Factory();
            UseWinRtComponent_StaticMethod();
            UseWinRtComponent_Throwing();

            Console.WriteLine();
            Console.WriteLine("> Now press ENTER to remove hooks and finish.");
            Console.ReadLine();
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //static void RegisterWinRtTypes(ManagedHooksManager.HooksManager hooksManager)
        //{
        //    hooksManager.RegisterWinRtType<WinRtComponent.NativeClass>();
        //}

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
    }
}
