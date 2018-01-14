#pragma once

#include "NativeClass.g.h"

namespace winrt::WinRtComponent::implementation
{
    struct NativeClass : NativeClassT<NativeClass>
    {
        NativeClass();
        NativeClass(const hstring& greeting);

        hstring Greeting();
        hstring GreetUser(const hstring& userName);
        hstring ToString();

        static void SetGreeting(const WinRtComponent::NativeClass& instance, const hstring& greeting);

    private:
        std::wstring m_greeting;
    };
}

namespace winrt::WinRtComponent::factory_implementation
{
    struct NativeClass : NativeClassT<NativeClass, implementation::NativeClass>
    {
    };
}
