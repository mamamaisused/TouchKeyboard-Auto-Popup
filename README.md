# TouchKeyboard-Auto-Popup
可以设置自动弹出win10的触摸键盘，前提是要先右键任务栏选上show touch keyboard button
目前这个方法可以适用部分程序，但是对于浏览器或者是基于electron开发的应用不行
因为浏览量访问的网页中的光标信息无法用user32.dll中的API访问
在Google中查到可以试试看用UI Automation去检测，不过似乎也挺有难度
https://stackoverflow.com/questions/49753666/how-to-tell-if-google-chrome-has-a-text-input-box-focused-under-windows
据说可以用inspect.exe查看每个窗口的UI
https://docs.microsoft.com/en-gb/windows/desktop/WinAuto/inspect-objects
