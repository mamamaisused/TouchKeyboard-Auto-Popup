Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading

''' <summary>
''' 可以自动设置自动弹出win10的触摸键盘，前提是要先右键任务栏选上show touch keyboard button
''' 目前这个方法可以适用部分程序，但是对于浏览器或者是基于electron开发的应用不行
''' 因为浏览量访问的网页中的光标信息无法用user32.dll中的API访问
''' 在Google中查到可以试试看用UI Automation去检测，不过似乎也挺有难度
''' https://stackoverflow.com/questions/49753666/how-to-tell-if-google-chrome-has-a-text-input-box-focused-under-windows
''' 据说可以用inspect.exe查看每个窗口的UI
''' https://docs.microsoft.com/en-gb/windows/desktop/WinAuto/inspect-objects
''' </summary>
''' <remarks></remarks>
Public Class Form1

    Const WM_LBUTTONDOWN = &H201
    Const WM_LBUTTONUP = &H202
    Const WS_VISIBLE = &H10000000
    Const GWL_STYLE = -16
    Const WM_SYSCOMMAND As Integer = &H112
    Const SC_CLOSE As Integer = &HF060
    Const WindowParentClass1709 = "ApplicationFrameWindow"
    Const WindowClass1709 = "Windows.UI.Core.CoreWindow"
    Const WindowCaption1709 = "Microsoft Text Input Application"

    Dim p As Process = New Process
    Dim autopopup As Boolean = False
    Dim focusptr As IntPtr
    Dim k As Integer
    Dim pp As New Point

#Region "Dll Import"

    <DllImport("user32.dll")> _
    Public Shared Function FindWindow(lpClassName As String, lpWindowName As String) As IntPtr
    End Function

    <DllImport("user32.dll")> _
    Public Shared Function PostMessage(hWnd As IntPtr, Msg As Integer, wParam As UInteger, lParam As UInteger) As Boolean
    End Function

    <DllImport("user32.dll")> _
    Public Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function
    <DllImport("user32.dll")> _
    Public Shared Function GetWindowLong(hWnd As IntPtr, nIndex As Integer) As UInteger
    End Function

    <DllImport("user32.dll")> _
    Public Shared Function GetFocus() As IntPtr
    End Function

    <DllImport("user32.dll")> _
    Public Shared Function GetCursor() As IntPtr
    End Function

    <DllImport("user32.dll")> _
    Public Shared Function GetCaretPos(ByRef lpPoint As Point) As Boolean
    End Function

    <DllImport("user32.dll")> _
    Private Shared Function GetForegroundWindow() As IntPtr
    End Function

    Declare Function AttachThreadInput Lib "user32" (ByVal idAttach As Integer, ByVal idAttachTo As Integer, ByVal fAttach As Boolean) As Integer
    <DllImport("user32.dll", SetLastError:=True)> _
    Private Shared Function GetWindowThreadProcessId(ByVal hwnd As IntPtr, _
                          ByRef lpdwProcessId As Integer) As Integer
    End Function

    <DllImport("user32.dll")> _
    Private Shared Function FindWindowEx(ByVal parentHandle As IntPtr, _
                      ByVal childAfter As IntPtr, _
                      ByVal lclassName As String, _
                      ByVal windowTitle As String) As IntPtr
    End Function

    Declare Function GetCurrentThreadId Lib "kernel32" Alias "GetCurrentThreadId" () As Integer

    <DllImport("user32.dll")> _
    Public Shared Function GetGUIThreadInfo(idThread As UInteger, ByRef lpgui As GUITHREADINFO) As Boolean
    End Function

#End Region

    ''' <summary>
    ''' 通过Process Start的方法调出touchkeyboard，前提是任务管理器中这个进程已经结束
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ShowTabTip()
        Debug.Print("show tabtip")
        Dim path As String = "C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe"
        p.StartInfo.FileName = path
        p.Start()
        Debug.Print("shown")
    End Sub

    ''' <summary>
    ''' 通过结束进程的方式关闭显示
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub HideTabTip()
        Dim TouchWhd As IntPtr = New IntPtr(0)
        TouchWhd = FindWindow("IPTip_Main_Window", Nothing)
        If TouchWhd <> IntPtr.Zero Then
            PostMessage(TouchWhd, WM_SYSCOMMAND, SC_CLOSE, 0)
        End If
    End Sub

    ''' <summary>
    ''' 范围当前插入符光标的位置，如果不在文本框内会返回（0，0）
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function caretposition() As Point
        Dim mp As New Point
        Dim ptr As IntPtr
        ptr = GetForegroundWindow()
        If ptr <> IntPtr.Zero Then
            Dim targetThreadId As IntPtr
            Dim currentThreadId As IntPtr = IntPtr.Zero
            targetThreadId = GetWindowThreadProcessId(ptr, IntPtr.Zero)
            currentThreadId = GetCurrentThreadId()
            If currentThreadId <> targetThreadId Then
                '如果不用attachthreadinput的话只能获取本程序的光标位置
                AttachThreadInput(currentThreadId, targetThreadId, True)
                ptr = GetFocus()
                Debug.Print("focus id: " + ptr.ToString)
                If ptr <> IntPtr.Zero Then
                    GetCaretPos(mp)
                End If
                AttachThreadInput(currentThreadId, targetThreadId, False)
            End If
        End If
        Return mp
    End Function

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        k += 1
        pp = caretposition()
        IsCaretExist()
        Debug.Print(k.ToString + ":" + pp.X.ToString + "," + pp.Y.ToString)
        If autopopup = True Then
            If pp.X <> 0 Or pp.Y <> 0 Then
                If Not IsTabTipOpen() Then
                    ClickTabTip_TaskBar()
                End If
            Else
                HideTabTip()
            End If
        End If
    End Sub



    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        If autopopup Then
            autopopup = False
            Button4.BackColor = Color.Gainsboro
            Button4.Text = "Auto PopUp Off"
            Timer1.Enabled = False
        Else
            autopopup = True
            Button4.Text = "Auto PopUp On"
            Button4.BackColor = Color.PaleTurquoise
            Timer1.Enabled = True
        End If
    End Sub



    ''' <summary>
    ''' 模拟鼠标按右下角Touch keyboard按键，必须右键任务栏在show touch keyboar button上打勾
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub ClickTabTip_TaskBar()
        Dim trayWnd As IntPtr
        Dim taskbarclass As String = "Shell_TrayWnd"
        Dim tabtipclass As String = "IPTip_Main_Window"
        trayWnd = FindWindow(taskbarclass, Nothing)
        If trayWnd <> 0 Then
            Debug.Print("task bar found. id = " + trayWnd.ToString)
            Dim trayNotifyWnd As IntPtr = FindWindowEx(trayWnd, IntPtr.Zero, "TrayNotifyWnd", Nothing)
            If trayNotifyWnd <> 0 Then
                Debug.Print("notify found.")
                Dim tipBandWnd As IntPtr = FindWindowEx(trayNotifyWnd, IntPtr.Zero, "TIPBand", Nothing)
                If tipBandWnd <> 0 Then
                    Debug.Print("tipband found")
                    PostMessage(tipBandWnd, WM_LBUTTONDOWN, 1, 65537)
                    PostMessage(tipBandWnd, WM_LBUTTONUP, 1, 65537)
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' 判断虚拟键盘是否显示
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function IsTabTipOpen() As Boolean
        Debug.Print("is open ?")
        Dim parent As IntPtr = IntPtr.Zero
        parent = FindWindowEx(IntPtr.Zero, parent, WindowParentClass1709, Nothing)
        If parent = IntPtr.Zero Then
            Debug.Print("no more windows, keyboard state is unknown") ' no more windows, keyboard state is unknown
            Return False
        Else
            'if it's a child of a WindowParentClass1709 window - the keyboard is open
            Dim wnd As IntPtr = FindWindowEx(parent, IntPtr.Zero, WindowClass1709, WindowCaption1709)
            If wnd <> IntPtr.Zero Then
                Return True
            Else
                Return False
            End If
        End If
    End Function

    Public Structure GUITHREADINFO
        Public cbSize As Integer
        Public flags As Integer
        Public hwndActive As IntPtr
        Public hwndFocus As IntPtr
        Public hwndCapture As IntPtr
        Public hwndMenuOwner As IntPtr
        Public hwndMoveSize As IntPtr
        Public hwndCaret As IntPtr
        Public rcCaret As Rectangle
    End Structure

    ''' <summary>
    ''' 通过GetGUIThreadInfo可以得到当前焦点窗口的数据，可以从guiThreadInfo中获取
    ''' 这也是一种可以判断是否存在插入符的方法
    ''' 在下面链接可以查到这个结构体的参数的含义
    ''' https://docs.microsoft.com/en-us/windows/desktop/api/winuser/ns-winuser-tagguithreadinfo
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function IsCaretExist() As Boolean
        Dim hwnd As IntPtr = GetForegroundWindow()
        If hwnd <> 0 Then
            Dim threadId As UInteger = GetWindowThreadProcessId(hwnd, IntPtr.Zero)
            Dim guiThreadInfo As New GUITHREADINFO
            '必须先分配大小，否则不能正确执行GetThreadInfo
            guiThreadInfo.cbSize = Marshal.SizeOf(guiThreadInfo)
            Debug.Print("Thread ID : " + threadId.ToString)
            If GetGUIThreadInfo(threadId, guiThreadInfo) Then
                Debug.Print("get gui thread info, hwndCaret is :" + guiThreadInfo.hwndCaret.ToString)
                Debug.Print("flags is :" + guiThreadInfo.flags.ToString("X8"))
                Debug.Print("hwndFocus is :" + guiThreadInfo.hwndFocus.ToString)
                Debug.Print("hwndCapture is :" + guiThreadInfo.hwndCapture.ToString)
            Else
                Debug.Print("!!gui thread not get")
            End If
        End If
        Return True
    End Function
End Class
