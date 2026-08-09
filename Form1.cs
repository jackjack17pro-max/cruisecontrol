using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CruiseControlApp
{
    public partial class Form1 : Form, IMessageFilter
    {
        private static readonly Version CurrentVersion = new Version(1, 0, 8);
        private const string GitHubRepoOwner = "jackjack17pro-max";
        private const string GitHubRepoName = "cruisecontrol";
        private const string RawPresetUrl = "https://raw.githubusercontent.com/jackjack17pro-max/cruisecontrol/main/config.ini";

        [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
        private static partial uint MapVirtualKey(uint uCode, uint uMapType);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW")]
        private static partial IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnhookWindowsHookEx(IntPtr hhk);

        [LibraryImport("user32.dll")]
        private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        private const int LLKHF_INJECTED = 0x00000010;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;

        private const uint XBUTTON1 = 0x0001;
        private const uint XBUTTON2 = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public System.Drawing.Point pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        private class TargetData
        {
            public int Target { get; set; }
            public int Delay { get; set; }
            public int Hold { get; set; }

            public Button? BtnTarget { get; set; }
            public NumericUpDown? NumDelay { get; set; }
            public NumericUpDown? NumHold { get; set; }
            public Button? BtnRemoveTarget { get; set; }
        }

        private class ProfileData
        {
            public int Trigger { get; set; }
            public bool IsPressMode { get; set; } = false;
            public List<TargetData> Targets { get; set; } = new List<TargetData>();

            public bool IsSequenceRunning { get; set; }

            public Label? LblName { get; set; }
            public Button? BtnTrigger { get; set; }
            public Button? BtnTriggerMode { get; set; }
            public Button? BtnAddTarget { get; set; }
            public Button? BtnDelete { get; set; }
            public Panel? RowPanel { get; set; }
        }

        private readonly List<ProfileData> profiles = new List<ProfileData>();
        private bool isAutoUpdateEnabled = true;
        private bool isDownloadingUpdate = false;

        private static readonly bool[] globalKeyState = new bool[256];
        private static readonly bool[] physicalKeyboardState = new bool[256];
        private static readonly int[] simulatedHoldCounts = new int[256];

        private IntPtr keyboardHookId = IntPtr.Zero;
        private IntPtr mouseHookId = IntPtr.Zero;

        private LowLevelProc? keyboardHookProc;
        private LowLevelProc? mouseHookProc;

        private Button? activeSettingButton;
        private readonly Random rng = new Random();
        private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        private readonly ToolTip toolTip = new ToolTip();

        private CancellationTokenSource? _macroCts;

        public Form1()
        {
            InitializeComponent();
            CleanupUpdater();

            KeyPreview = true;
            Text = "CruiseControlByJACKNOVA_" + Guid.NewGuid().ToString().Substring(0, 8);

            LoadConfig();
            RebuildUI();

            Application.AddMessageFilter(this);

            keyboardHookProc = KeyboardHookCallback;
            mouseHookProc = MouseHookCallback;

            HandleCreated += Form1_HandleCreated;
            FormClosed += Form1_FormClosed;

            _macroCts = new CancellationTokenSource();
            Task.Run(() => MacroLoop(_macroCts.Token));

            if (isAutoUpdateEnabled)
            {
                _ = CheckForUpdatesAsync(isSilent: true);
            }
        }

        private static void CleanupUpdater()
        {
            try
            {
                string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.bat");
                if (File.Exists(batPath))
                {
                    File.Delete(batPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CleanupUpdater Error] {ex.Message}");
            }
        }

        private void Form1_HandleCreated(object? sender, EventArgs e)
        {
            if (keyboardHookId == IntPtr.Zero)
            {
                keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookProc!, IntPtr.Zero, 0);
            }
            if (mouseHookId == IntPtr.Zero)
            {
                mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, mouseHookProc!, IntPtr.Zero, 0);
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_XBUTTONDOWN && activeSettingButton != null)
            {
                int xbutton = ((int)m.WParam >> 16) & 0xFFFF;
                int vKey = (xbutton == 1) ? 0x05 : 0x06;

                AssignKey(vKey);
                return true;
            }
            return false;
        }

        private unsafe IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                int msg = (int)wParam;
                int vKey = 0;
                bool isDown = false;
                bool isMouseKeyMsg = false;

                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP)
                {
                    vKey = (int)Keys.LButton;
                    isDown = (msg == WM_LBUTTONDOWN);
                    isMouseKeyMsg = true;
                }
                else if (msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP)
                {
                    vKey = (int)Keys.RButton;
                    isDown = (msg == WM_RBUTTONDOWN);
                    isMouseKeyMsg = true;
                }
                else if (msg == WM_MBUTTONDOWN || msg == WM_MBUTTONUP)
                {
                    vKey = (int)Keys.MButton;
                    isDown = (msg == WM_MBUTTONDOWN);
                    isMouseKeyMsg = true;
                }
                else if (msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP)
                {
                    MSLLHOOKSTRUCT* msh = (MSLLHOOKSTRUCT*)lParam;
                    int xbutton = (msh->mouseData >> 16) & 0xFFFF;
                    vKey = (xbutton == 1) ? 0x05 : 0x06;
                    isDown = (msg == WM_XBUTTONDOWN);
                    isMouseKeyMsg = true;
                }

                if (isMouseKeyMsg)
                {
                    globalKeyState[vKey] = isDown;

                    if (isDown && activeSettingButton != null)
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            int keyToAssign = vKey;
                            BeginInvoke(new Action(() => AssignKey(keyToAssign)));
                        }
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        private unsafe IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                KBDLLHOOKSTRUCT* kbd = (KBDLLHOOKSTRUCT*)lParam;
                bool isVirtual = (kbd->flags & LLKHF_INJECTED) != 0;

                if (!isVirtual && kbd->vkCode >= 0 && kbd->vkCode < 256)
                {
                    int msg = (int)wParam;
                    int vk = kbd->vkCode;

                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        globalKeyState[vk] = true;
                        physicalKeyboardState[vk] = true;

                        if (vk == (int)Keys.LShiftKey || vk == (int)Keys.RShiftKey || vk == (int)Keys.ShiftKey)
                            physicalKeyboardState[(int)Keys.ShiftKey] = true;
                        if (vk == (int)Keys.LControlKey || vk == (int)Keys.RControlKey || vk == (int)Keys.ControlKey)
                            physicalKeyboardState[(int)Keys.ControlKey] = true;
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        globalKeyState[vk] = false;
                        physicalKeyboardState[vk] = false;

                        if (vk == (int)Keys.LShiftKey || vk == (int)Keys.RShiftKey || vk == (int)Keys.ShiftKey)
                            physicalKeyboardState[(int)Keys.ShiftKey] = false;
                        if (vk == (int)Keys.LControlKey || vk == (int)Keys.RControlKey || vk == (int)Keys.ControlKey)
                            physicalKeyboardState[(int)Keys.ControlKey] = false;

                        // БЕСШОВНЫЙ ПЕРЕХОД: Блокируем отпускание клавиши для ОС, если макрос удерживает эту клавишу
                        if (simulatedHoldCounts[vk] > 0 ||
                           (vk == (int)Keys.LShiftKey && simulatedHoldCounts[(int)Keys.ShiftKey] > 0) ||
                           (vk == (int)Keys.LControlKey && simulatedHoldCounts[(int)Keys.ControlKey] > 0))
                        {
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        private async Task MacroLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (activeSettingButton != null)
                {
                    await Task.Delay(15, token);
                    continue;
                }

                bool anyActive = false;

                ProfileData[] localProfiles;
                lock (profiles)
                {
                    localProfiles = profiles.ToArray();
                }

                foreach (var profile in localProfiles)
                {
                    if (ProcessProfile(profile))
                        anyActive = true;
                }

                int delay = anyActive ? rng.Next(15, 30) : 15;
                await Task.Delay(delay, token);
            }
        }

        private static bool ProcessProfile(ProfileData profile)
        {
            if (profile.Trigger == 0) return false;

            bool isTriggerPressed = globalKeyState[profile.Trigger];

            if (isTriggerPressed && !profile.IsSequenceRunning)
            {
                profile.IsSequenceRunning = true;
                _ = RunSequenceAsync(profile);
                return true;
            }

            return profile.IsSequenceRunning;
        }

        private static async Task RunSequenceAsync(ProfileData profile)
        {
            try
            {
                TargetData[] targetsCopy;
                lock (profile.Targets)
                {
                    targetsCopy = profile.Targets.ToArray();
                }

                foreach (var targetItem in targetsCopy)
                {
                    if (targetItem.Target == 0) continue;

                    if (targetItem.Delay > 0) await Task.Delay(targetItem.Delay);
                    if (!profile.IsPressMode && !globalKeyState[profile.Trigger]) return;

                    SimulateKey(targetItem.Target, false);
                    _ = ScheduleKeyRelease(targetItem.Target, targetItem.Hold, profile.Trigger, profile.IsPressMode);
                }

                if (!profile.IsPressMode)
                {
                    while (globalKeyState[profile.Trigger])
                    {
                        await Task.Delay(10);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RunSequenceAsync Error] {ex.Message}");
            }
            finally
            {
                profile.IsSequenceRunning = false;
            }
        }

        private static async Task ScheduleKeyRelease(int targetKey, int holdDurationMs, int triggerKey, bool isPressMode)
        {
            try
            {
                if (holdDurationMs > 0)
                {
                    int elapsed = 0;
                    while (elapsed < holdDurationMs && (isPressMode || globalKeyState[triggerKey]))
                    {
                        await Task.Delay(10);
                        elapsed += 10;
                    }
                }
                else
                {
                    if (isPressMode)
                    {
                        await Task.Delay(20);
                    }
                    else
                    {
                        while (globalKeyState[triggerKey])
                        {
                            await Task.Delay(10);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScheduleKeyRelease Error] {ex.Message}");
            }
            finally
            {
                SimulateKey(targetKey, true);
            }
        }

        private static void SimulateKey(int vKey, bool keyUp)
        {
            int mappedKey = vKey;
            if (mappedKey == (int)Keys.ShiftKey) mappedKey = (int)Keys.LShiftKey;
            if (mappedKey == (int)Keys.ControlKey) mappedKey = (int)Keys.LControlKey;

            if (vKey >= 0 && vKey < 256)
            {
                if (!keyUp)
                {
                    Interlocked.Increment(ref simulatedHoldCounts[vKey]);
                    if (mappedKey != vKey) Interlocked.Increment(ref simulatedHoldCounts[mappedKey]);
                }
                else
                {
                    if (simulatedHoldCounts[vKey] > 0)
                        Interlocked.Decrement(ref simulatedHoldCounts[vKey]);
                    if (mappedKey != vKey && simulatedHoldCounts[mappedKey] > 0)
                        Interlocked.Decrement(ref simulatedHoldCounts[mappedKey]);

                    // БЕСШОВНЫЙ ПЕРЕХОД: Если физическая клавиша всё ещё удерживается пальцем — не отжимаем её в системе!
                    if (physicalKeyboardState[vKey] || physicalKeyboardState[mappedKey])
                    {
                        return;
                    }
                }
            }

            if (vKey == (int)Keys.LButton)
            {
                mouse_event(keyUp ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                return;
            }
            if (vKey == (int)Keys.RButton)
            {
                mouse_event(keyUp ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                return;
            }
            if (vKey == (int)Keys.MButton)
            {
                mouse_event(keyUp ? MOUSEEVENTF_MIDDLEUP : MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
                return;
            }
            if (vKey == 0x05)
            {
                mouse_event(keyUp ? MOUSEEVENTF_XUP : MOUSEEVENTF_XDOWN, 0, 0, XBUTTON1, UIntPtr.Zero);
                return;
            }
            if (vKey == 0x06)
            {
                mouse_event(keyUp ? MOUSEEVENTF_XUP : MOUSEEVENTF_XDOWN, 0, 0, XBUTTON2, UIntPtr.Zero);
                return;
            }

            byte scanCode = (byte)MapVirtualKey((uint)mappedKey, 0);
            uint flags = 0;

            if (mappedKey == (int)Keys.RShiftKey || mappedKey == (int)Keys.RControlKey)
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }

            if (keyUp) flags |= KEYEVENTF_KEYUP;

            keybd_event((byte)mappedKey, scanCode, flags, UIntPtr.Zero);
        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (activeSettingButton != null)
            {
                AssignKey((int)e.KeyCode);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void AssignKey(int vKey)
        {
            if (activeSettingButton == null) return;

            lock (profiles)
            {
                foreach (var p in profiles)
                {
                    if (activeSettingButton == p.BtnTrigger)
                    {
                        p.Trigger = vKey;
                        break;
                    }
                    foreach (var t in p.Targets)
                    {
                        if (activeSettingButton == t.BtnTarget)
                        {
                            t.Target = vKey;
                            break;
                        }
                    }
                }
            }

            activeSettingButton = null;
            SaveConfig();
            UpdateLabels();
        }

        private void SetButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                activeSettingButton = button;
                activeSettingButton.Text = "[Нажмите]";
            }
        }

        private void BtnAddProfile_Click(object? sender, EventArgs e)
        {
            lock (profiles)
            {
                profiles.Add(CreateDefaultProfile());
            }
            SaveConfig();
            RebuildUI();
        }

        private ProfileData CreateDefaultProfile()
        {
            var p = new ProfileData();
            p.Targets.Add(new TargetData());
            p.Targets.Add(new TargetData());
            p.Targets.Add(new TargetData());
            return p;
        }

        private void BtnDeleteProfile_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProfileData data)
            {
                lock (profiles)
                {
                    profiles.Remove(data);
                }
                SaveConfig();
                RebuildUI();
            }
        }

        private void BtnReset_Click(object? sender, EventArgs e)
        {
            lock (profiles)
            {
                profiles.Clear();
                profiles.Add(CreateDefaultProfile());
                profiles.Add(CreateDefaultProfile());
            }
            SaveConfig();
            RebuildUI();
        }

        private void BtnHelp_Click(object? sender, EventArgs e)
        {
            MessageBox.Show(
                $@"Утилита Круиз-контроль By Jack Nova (v{CurrentVersion})

1. Назначение программы:
Программа предназначена для создания макросов и автоматизации комбинаций клавиш с гибкой настройкой задержек и длительности удержания.

2. Настройка профиля:
• Профиль: Каждая строка представляет собой отдельный независимый макрос.
• Клавиша-активатор (Триггер): Первая кнопка в строке («Выбрать»). Нажмите её и зажмите нужную клавишу на клавиатуре или боковую кнопку мыши (XButton1 / XButton2).
• Режим активатора («Зажатие / Нажатие»): 
  - Зажатие — цепочка выполняет действия, пока вы физически держите клавишу.
  - Нажатие — один клик запускает всю цепочку до конца без необходимости держать клавишу.
• Прожимаемые клавиши: Кнопки задают последовательность клавиш, которые программа отправляет в систему/игру.

3. Параметры клавиш:
• Первое число (Задержка, мс): Пауза в миллисекундах перед нажатием этой клавиши.
• Второе число (Удержание, мс): Сколько миллисекунд держать клавишу нажатой:
  - 0 — клавиша удерживается нажатой всё время, пока зажат активатор (или импульсный клик в режиме «Нажатие»).
  - > 0 (например, 700) — клавиша зажимается ровно на указанное время (в мс) и автоматически отпускается.

4. Управление элементами:
• Кнопка «+»: Добавляет дополнительную прожимаемую клавишу в цепочку профиля.
• Кнопка «-»: Удаляет конкретную прожимаемую клавишу из цепочки.
• Кнопка «X»: Полностью удаляет весь профиль.
• «Добавить профиль»: Создаёт новую строку макроса внизу списка.
• «Сбросить всё»: Очищает все настройки и возвращает программу к начальным двум профилям.
• «Проверить обновления»: Ручной поиск свежей версии программы на GitHub.
• «Скачать пресет»: Скачивание эталонного конфига с сервера.

5. Подсказки:
При наведении курсора мыши на любой элемент интерфейса вы увидите всплывающую подсказку с его назначением. Все настройки сохраняются автоматически в файл config.ini.",
                "Справка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private async void BtnCheckUpdate_Click(object? sender, EventArgs e)
        {
            if (isDownloadingUpdate)
            {
                MessageBox.Show("Обновление уже скачивается!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnCheckUpdate.Enabled = false;
            await CheckForUpdatesAsync(isSilent: false);
            btnCheckUpdate.Enabled = true;
        }

        private async void BtnLoadPreset_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Все ваши текущие настройки профилей будут заменены пресетом с сервера.\nПродолжить?",
                "Загрузка пресета",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes) return;

            try
            {
                using var client = new HttpClient();
                string newConfigText = await client.GetStringAsync(RawPresetUrl);

                if (!string.IsNullOrWhiteSpace(newConfigText))
                {
                    await File.WriteAllTextAsync(configPath, newConfigText);
                    LoadConfig();
                    RebuildUI();
                    MessageBox.Show("Пресет успешно загружен и применён!", "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось скачать пресет: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CbAutoUpdate_CheckedChanged(object? sender, EventArgs e)
        {
            isAutoUpdateEnabled = cbAutoUpdate.Checked;
            SaveConfig();
        }

        private void RebuildUI()
        {
            flpProfiles.SuspendLayout();
            flpProfiles.Controls.Clear();

            toolTip.RemoveAll();

            toolTip.SetToolTip(btnAddProfile, "Добавить новый профиль в конец списка");
            toolTip.SetToolTip(btnReset, "Сбросить все профили к начальным настройкам");
            toolTip.SetToolTip(btnHelp, "Открыть краткое руководство");
            toolTip.SetToolTip(btnCheckUpdate, "Проверить наличие новой версии программы на GitHub");
            toolTip.SetToolTip(btnLoadPreset, "Загрузить эталонный пресет профилей с сервера");
            toolTip.SetToolTip(cbAutoUpdate, "Автоматически проверять и скачивать обновления при запуске");

            cbAutoUpdate.Checked = isAutoUpdateEnabled;

            ProfileData[] localProfiles;
            lock (profiles)
            {
                localProfiles = profiles.ToArray();
            }

            int maxRowWidth = 0;

            for (int i = 0; i < localProfiles.Length; i++)
            {
                var p = localProfiles[i];

                int xPos = 5;
                p.RowPanel = new Panel { Height = 32, Margin = new Padding(0) };

                p.LblName = new Label { Text = $"Профиль {i + 1}:", Location = new System.Drawing.Point(xPos, 7), Size = new System.Drawing.Size(75, 13) };
                xPos += 80;

                p.BtnTrigger = new Button { Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(85, 23), UseVisualStyleBackColor = true };
                p.BtnTrigger.Click += SetButton_Click;
                toolTip.SetToolTip(p.BtnTrigger, "Клавиша-активатор (триггер макроса)");
                xPos += 90;

                p.BtnTriggerMode = new Button { Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(75, 23), UseVisualStyleBackColor = true, Tag = p };
                p.BtnTriggerMode.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is ProfileData profileObj)
                    {
                        profileObj.IsPressMode = !profileObj.IsPressMode;
                        SaveConfig();
                        UpdateLabels();
                    }
                };
                toolTip.SetToolTip(p.BtnTriggerMode, "Режим активации: Зажатие (удерживать триггер) / Нажатие (одиночный клик для запуска)");
                xPos += 80;

                p.RowPanel.Controls.Add(p.LblName);
                p.RowPanel.Controls.Add(p.BtnTrigger);
                p.RowPanel.Controls.Add(p.BtnTriggerMode);

                for (int j = 0; j < p.Targets.Count; j++)
                {
                    var t = p.Targets[j];

                    t.BtnTarget = new Button { Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(65, 23), UseVisualStyleBackColor = true };
                    t.BtnTarget.Click += SetButton_Click;
                    toolTip.SetToolTip(t.BtnTarget, "Прожимаемая клавиша");
                    xPos += 70;

                    t.NumDelay = new NumericUpDown { Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(48, 23), Minimum = 0, Maximum = 9999, Value = t.Delay };
                    t.NumDelay.ValueChanged += (s, e) => { t.Delay = (int)t.NumDelay.Value; SaveConfig(); };
                    toolTip.SetToolTip(t.NumDelay, "Задержка перед нажатием клавиши (в миллисекундах)");
                    xPos += 52;

                    t.NumHold = new NumericUpDown { Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(48, 23), Minimum = 0, Maximum = 9999, Value = t.Hold };
                    t.NumHold.ValueChanged += (s, e) => { t.Hold = (int)t.NumHold.Value; SaveConfig(); };
                    toolTip.SetToolTip(t.NumHold, "Длительность удержания клавиши (мс). 0 = держать пока зажат активатор");
                    xPos += 52;

                    if (p.Targets.Count > 1)
                    {
                        t.BtnRemoveTarget = new Button { Text = "-", Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(22, 23), UseVisualStyleBackColor = true, Tag = (p, t) };
                        t.BtnRemoveTarget.Click += (s, e) =>
                        {
                            if (s is Button b && b.Tag is ValueTuple<ProfileData, TargetData> tuple)
                            {
                                tuple.Item1.Targets.Remove(tuple.Item2);
                                SaveConfig();
                                RebuildUI();
                            }
                        };
                        toolTip.SetToolTip(t.BtnRemoveTarget, "Удалить эту прожимаемую клавишу");
                        p.RowPanel.Controls.Add(t.BtnRemoveTarget);
                        xPos += 26;
                    }

                    p.RowPanel.Controls.Add(t.BtnTarget);
                    p.RowPanel.Controls.Add(t.NumDelay);
                    p.RowPanel.Controls.Add(t.NumHold);

                    xPos += 10;
                }

                p.BtnAddTarget = new Button { Text = "+", Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(26, 23), UseVisualStyleBackColor = true, Tag = p };
                p.BtnAddTarget.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is ProfileData profileObj)
                    {
                        profileObj.Targets.Add(new TargetData());
                        SaveConfig();
                        RebuildUI();
                    }
                };
                toolTip.SetToolTip(p.BtnAddTarget, "Добавить новую прожимаемую клавишу в этот профиль");
                p.RowPanel.Controls.Add(p.BtnAddTarget);
                xPos += 32;

                p.BtnDelete = new Button { Text = "X", Location = new System.Drawing.Point(xPos, 3), Size = new System.Drawing.Size(28, 23), UseVisualStyleBackColor = true, Tag = p };
                p.BtnDelete.Click += BtnDeleteProfile_Click;
                toolTip.SetToolTip(p.BtnDelete, "Удалить весь профиль");
                p.RowPanel.Controls.Add(p.BtnDelete);
                xPos += 35;

                p.RowPanel.Width = xPos;
                if (xPos > maxRowWidth) maxRowWidth = xPos;

                flpProfiles.Controls.Add(p.RowPanel);
            }

            UpdateLabels();
            flpProfiles.ResumeLayout(true);

            int totalProfilesHeight = localProfiles.Length * 32;
            int totalClientHeight = pnlTopControls.Height + totalProfilesHeight + flpProfiles.Padding.Top + flpProfiles.Padding.Bottom;
            int totalClientWidth = Math.Max(maxRowWidth + 20, 810);

            ClientSize = new System.Drawing.Size(totalClientWidth, Math.Max(totalClientHeight, pnlTopControls.Height + 40));
        }

        private void UpdateLabels()
        {
            ProfileData[] localProfiles;
            lock (profiles)
            {
                localProfiles = profiles.ToArray();
            }

            foreach (var p in localProfiles)
            {
                if (p.BtnTrigger != null) p.BtnTrigger.Text = p.Trigger == 0 ? "Выбрать" : GetKeyName(p.Trigger);
                if (p.BtnTriggerMode != null) p.BtnTriggerMode.Text = p.IsPressMode ? "Нажатие" : "Зажатие";

                foreach (var t in p.Targets)
                {
                    if (t.BtnTarget != null) t.BtnTarget.Text = t.Target == 0 ? "Выбрать" : GetKeyName(t.Target);
                }
            }
        }

        private static string GetKeyName(int vKey)
        {
            if (vKey == 0x01) return "LButton";
            if (vKey == 0x02) return "RButton";
            if (vKey == 0x04) return "MButton";
            if (vKey == 0x05) return "XButton1";
            if (vKey == 0x06) return "XButton2";
            return ((Keys)vKey).ToString();
        }

        private async Task CheckForUpdatesAsync(bool isSilent)
        {
            if (isDownloadingUpdate)
            {
                if (!isSilent) MessageBox.Show("Обновление уже скачивается!", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CruiseControlApp", CurrentVersion.ToString()));

                string url = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";
                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    if (!isSilent) MessageBox.Show("Не удалось получить данные с GitHub.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagElement)) return;
                string rawTag = tagElement.GetString() ?? "";
                string versionStr = rawTag.TrimStart('v', 'V');

                if (!Version.TryParse(versionStr, out var latestVersion)) return;

                if (latestVersion > CurrentVersion)
                {
                    string downloadUrl = "";
                    if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assetsElement.EnumerateArray())
                        {
                            if (asset.TryGetProperty("name", out var nameProp) &&
                                nameProp.GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                if (asset.TryGetProperty("browser_download_url", out var urlProp))
                                {
                                    downloadUrl = urlProp.GetString() ?? "";
                                    break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl)) return;

                    bool shouldUpdate = false;
                    if (isSilent)
                    {
                        shouldUpdate = true;
                    }
                    else
                    {
                        var result = MessageBox.Show(
                            $"Доступна новая версия {rawTag}!\nХотите обновить программу прямо сейчас?",
                            "Обновление найдено",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );
                        shouldUpdate = (result == DialogResult.Yes);
                    }

                    if (shouldUpdate)
                    {
                        await ApplyUpdateAsync(downloadUrl);
                    }
                }
                else if (!isSilent)
                {
                    MessageBox.Show("У вас установлена последняя версия программы.", "Обновлений нет", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckForUpdatesAsync Error] {ex.Message}");
                if (!isSilent) MessageBox.Show($"Ошибка проверки обновлений: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ApplyUpdateAsync(string downloadUrl)
        {
            if (isDownloadingUpdate) return;

            isDownloadingUpdate = true;
            btnCheckUpdate.Enabled = false;

            try
            {
                pbDownload.Value = 0;
                lblDownloadProgress.Text = "0%";
                pbDownload.Visible = true;
                lblDownloadProgress.Visible = true;

                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Application.ExecutablePath;
                string newExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CruiseControl_new.exe");
                string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.bat");

                using (var client = new HttpClient())
                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(newExe, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0L;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                int percentage = (int)((double)totalRead / totalBytes * 100);
                                Invoke(new Action(() =>
                                {
                                    pbDownload.Value = Math.Min(100, percentage);
                                    lblDownloadProgress.Text = $"{percentage}%";
                                }));
                            }
                        }
                    }
                }

                string script = $@"@echo off
chcp 866 > nul
timeout /t 1 /nobreak > nul
move /y ""{newExe}"" ""{currentExe}""
start """" ""{currentExe}""
del ""%~f0""";

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var cp866 = Encoding.GetEncoding(866);
                await File.WriteAllTextAsync(batPath, script, cp866);

                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                Application.Exit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplyUpdateAsync Error] {ex.Message}");
                isDownloadingUpdate = false;
                btnCheckUpdate.Enabled = true;
                pbDownload.Visible = false;
                lblDownloadProgress.Visible = false;
                MessageBox.Show($"Не удалось выполнить обновление: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadConfig()
        {
            if (!File.Exists(configPath))
            {
                lock (profiles)
                {
                    profiles.Add(CreateDefaultProfile());
                    profiles.Add(CreateDefaultProfile());
                }
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(configPath);
                lock (profiles)
                {
                    profiles.Clear();
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.StartsWith("AutoUpdate=", StringComparison.OrdinalIgnoreCase))
                        {
                            bool.TryParse(line.Substring(11), out isAutoUpdateEnabled);
                            continue;
                        }

                        string[] parts = line.Split('|');

                        var p = new ProfileData();
                        if (int.TryParse(parts[0], out int t)) p.Trigger = t;

                        int targetStartIdx = 1;
                        if (parts.Length > 1 && (parts[1] == "1" || parts[1] == "0" || bool.TryParse(parts[1], out _)))
                        {
                            p.IsPressMode = parts[1] == "1" || parts[1].Equals("true", StringComparison.OrdinalIgnoreCase);
                            targetStartIdx = 2;
                        }

                        if (parts.Length > targetStartIdx && parts[targetStartIdx].Contains(';'))
                        {
                            for (int i = targetStartIdx; i < parts.Length; i++)
                            {
                                string[] targetProps = parts[i].Split(';');
                                if (targetProps.Length == 3)
                                {
                                    var td = new TargetData();
                                    if (int.TryParse(targetProps[0], out int tg)) td.Target = tg;
                                    if (int.TryParse(targetProps[1], out int d)) td.Delay = d;
                                    if (int.TryParse(targetProps[2], out int h)) td.Hold = h;
                                    p.Targets.Add(td);
                                }
                            }
                        }

                        if (p.Targets.Count == 0)
                        {
                            p.Targets.Add(new TargetData());
                        }

                        profiles.Add(p);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadConfig Error] {ex.Message}");
            }

            lock (profiles)
            {
                if (profiles.Count == 0)
                {
                    profiles.Add(CreateDefaultProfile());
                    profiles.Add(CreateDefaultProfile());
                }
            }
        }

        private void SaveConfig()
        {
            try
            {
                List<string> lines = new List<string>
                {
                    $"AutoUpdate={isAutoUpdateEnabled}"
                };

                lock (profiles)
                {
                    foreach (var p in profiles)
                    {
                        List<string> targetStrings = new List<string>();
                        foreach (var t in p.Targets)
                        {
                            targetStrings.Add($"{t.Target};{t.Delay};{t.Hold}");
                        }
                        lines.Add($"{p.Trigger}|{(p.IsPressMode ? "1" : "0")}|{string.Join("|", targetStrings)}");
                    }
                }
                File.WriteAllLines(configPath, lines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveConfig Error] {ex.Message}");
            }
        }

        private void Form1_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _macroCts?.Cancel();
            _macroCts?.Dispose();

            Application.RemoveMessageFilter(this);

            if (keyboardHookId != IntPtr.Zero)
                UnhookWindowsHookEx(keyboardHookId);

            if (mouseHookId != IntPtr.Zero)
                UnhookWindowsHookEx(mouseHookId);
        }
    }
}