#include "pch.h"
#include "NativeClass.h"

namespace winrt::WinRtComponent::implementation
{
    NativeClass::NativeClass()
        : m_greeting(L"Howdy %s, how's your day going?")
    {
    }

    NativeClass::NativeClass(const hstring& greeting)
        : m_greeting(greeting)
    {
    }

    hstring NativeClass::Greeting()
    {
        return hstring(m_greeting);
    }

    hstring NativeClass::GreetUser(const hstring& userName)
    {
        if (userName.empty())
            throw hresult_invalid_argument();

        int capacity = _scwprintf(m_greeting.c_str(), userName.c_str()) + 1; // add space for terminating null
		std::unique_ptr<wchar_t[]> buf = std::make_unique<wchar_t[]>(capacity);
        int size = _snwprintf_s(buf.get(), capacity, _TRUNCATE, m_greeting.c_str(), userName.c_str());

        return hstring(buf.get(), size);
    }

    hstring NativeClass::ToString()
    {
		return GetRuntimeClassName();
    }

    void NativeClass::SetGreeting(const WinRtComponent::NativeClass& instance, const hstring& greeting)
    {
        from_abi<NativeClass>(instance)->m_greeting = greeting;
    }
}
