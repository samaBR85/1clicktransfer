using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Views;

public partial class MainWindow : Window
{
    // Avalonia não tem RestoreBounds: rastreamos o último retângulo Normal manualmente.
    private double _normalX, _normalY, _normalW, _normalH;
    private bool _hiddenToTray;

    private static AppSettings S => App.Settings;
    private MainViewModel? Vm => DataContext as MainViewModel;

    // ColumnDefinition/RowDefinition não geram campos x:Name em Avalonia → acesso por índice.
    private ColumnDefinition ColSrc => CardsGrid.ColumnDefinitions[0];
    private ColumnDefinition ColDst => CardsGrid.ColumnDefinitions[2];
    private RowDefinition RowTasks => RootGrid.RowDefinitions[1];
    private RowDefinition RowQueue => RootGrid.RowDefinitions[10];

    public MainWindow()
    {
        InitializeComponent();
        TryRestoreBounds();
        ApplyLayout();

        // F4 (atalho configurável) / F5 (refresh) — tunnel p/ ganhar das listas.
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);

        LayoutUpdated += (_, _) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _normalX = Position.X; _normalY = Position.Y;
                _normalW = Width; _normalH = Height;
            }
        };

        Opened += (_, _) =>
        {
            WindowsDarkTitleBar.Apply(this, S.Theme != "light");
            EnsureOnScreen();
            Vm?.OnOpened(fromTrayRestore: _hiddenToTray);
        };
        Closing += OnClosing;
        Closed += (_, _) => Vm?.OnClosed();
    }

    /// <summary>Chamado ao restaurar da bandeja (clique no ícone/menu "Abrir"). Não depende de
    /// o Avalonia disparar Opened de novo em Show() após Hide() -- garante que o VM saiba que
    /// isto é uma restauração, não a primeira abertura da janela.</summary>
    public void RestoreFromTray()
    {
        var wasHidden = _hiddenToTray;
        Show();
        WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        Activate();
        if (wasHidden)
        {
            Vm?.OnOpened(fromTrayRestore: true);
            _hiddenToTray = false;
        }
    }

    // Aplica a razão do divisor (0.15–0.85) e a altura do painel TAREFAS (140–600) salvas.
    private void ApplyLayout()
    {
        var r = S.SplitRatio;
        if (r < 0.15) r = 0.15; else if (r > 0.85) r = 0.85;
        ColSrc.Width = new GridLength(r, GridUnitType.Star);
        ColDst.Width = new GridLength(1 - r, GridUnitType.Star);

        var h = S.TasksHeight;
        if (double.IsNaN(h) || h < 140) h = 150;
        if (h > 600) h = 600;
        RowTasks.Height = new GridLength(h, GridUnitType.Pixel);

        var qh = S.QueueHeight;
        if (double.IsNaN(qh) || qh < 90) qh = 160;
        if (qh > 500) qh = 500;
        RowQueue.Height = new GridLength(qh, GridUnitType.Pixel);
    }

    // ---------------- Geometria ----------------
    private void TryRestoreBounds()
    {
        if (S.WindowWidth >= 400 && S.WindowHeight >= 400)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = S.WindowWidth; Height = S.WindowHeight;
            Position = new PixelPoint((int)S.WindowLeft, (int)S.WindowTop);
        }
        if (S.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void SaveBounds()
    {
        try
        {
            if (WindowState == WindowState.Normal)
            {
                S.WindowLeft = Position.X; S.WindowTop = Position.Y;
                S.WindowWidth = Width; S.WindowHeight = Height;
                S.WindowMaximized = false;
            }
            else
            {
                if (_normalW > 0 && _normalH > 0)
                {
                    S.WindowLeft = _normalX; S.WindowTop = _normalY;
                    S.WindowWidth = _normalW; S.WindowHeight = _normalH;
                }
                S.WindowMaximized = WindowState == WindowState.Maximized;
            }
            SettingsService.Save(S);
        }
        catch { }
    }

    // Fechar de verdade sempre encerra; minimizar-pra-bandeja é opt-in (checkbox em Configurações).
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (S.MinimizeToTrayOnClose && !App.IsReallyExiting)
        {
            e.Cancel = true;
            _hiddenToTray = true;
            Hide();
            return;
        }
        SaveBounds();
    }

    // Reposiciona no centro da tela primária se a janela restaurada ficou fora de qualquer monitor.
    private void EnsureOnScreen()
    {
        if (WindowState != WindowState.Normal) return;
        var screens = Screens;
        if (screens?.All == null || screens.All.Count == 0) return;
        var p = Position;
        bool visible = screens.All.Any(s =>
        {
            var b = s.Bounds;
            return p.X < b.X + b.Width - 60 && p.X + 100 > b.X
                && p.Y < b.Y + b.Height - 40 && p.Y + 40 > b.Y;
        });
        if (visible) return;
        var wa = (screens.Primary ?? screens.All[0]).WorkingArea;
        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        int w = (int)(Width * scale), h = (int)(Height * scale);
        Position = new PixelPoint(wa.X + Math.Max(0, (wa.Width - w) / 2), wa.Y + Math.Max(0, (wa.Height - h) / 2));
    }

    // ---------------- Splitters (persistência de layout) ----------------
    private void Splitter_DragCompleted(object? sender, VectorEventArgs e)
    {
        var total = ColSrc.ActualWidth + ColDst.ActualWidth;
        if (total > 0)
        {
            var r = ColSrc.ActualWidth / total;
            if (r < 0.15) r = 0.15; else if (r > 0.85) r = 0.85;
            S.SplitRatio = r;
            SettingsService.Save(S);
        }
    }

    private void TasksSplitter_DragCompleted(object? sender, VectorEventArgs e)
    {
        if (RowTasks.ActualHeight >= 52)
        {
            S.TasksHeight = RowTasks.ActualHeight;
            SettingsService.Save(S);
        }
    }

    // Arrasto manual (não GridSplitter): entre a fila e o card acima há várias rows Auto
    // (ação/transferir/progresso/status/taxa) e o GridSplitter atravessa elas, redimensionando
    // o card SOURCE/DESTINATION em vez da fila. Arrastar pra cima cresce a fila (puxa o topo
    // dela pra cima); pra baixo encolhe.
    private bool _queueDragging;
    private double _queueDragStartY;
    private double _queueDragStartHeight;

    private void QueueSplitter_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _queueDragging = true;
        _queueDragStartY = e.GetPosition(this).Y;
        _queueDragStartHeight = RowQueue.ActualHeight;
        e.Pointer.Capture(sender as Border);
    }

    private void QueueSplitter_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_queueDragging) return;
        var dy = e.GetPosition(this).Y - _queueDragStartY;
        var h = _queueDragStartHeight - dy;
        if (h < 90) h = 90; else if (h > 500) h = 500;
        RowQueue.Height = new GridLength(h, GridUnitType.Pixel);
    }

    private void QueueSplitter_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_queueDragging) return;
        _queueDragging = false;
        e.Pointer.Capture(null);
        S.QueueHeight = RowQueue.ActualHeight;
        SettingsService.Save(S);
    }

    // ---------------- Navegação (duplo-clique) ----------------
    // Resolve a linha pela árvore visual (não depende de seleção — as grades não selecionam).
    private static FileRow? RowUnder(TappedEventArgs e)
        => (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<DataGridRow>().FirstOrDefault()?.DataContext as FileRow;

    private void GridSrc_DoubleTapped(object? sender, TappedEventArgs e)
        => Vm?.Source.NavigateCommand.Execute(RowUnder(e));

    private void GridDst_DoubleTapped(object? sender, TappedEventArgs e)
        => Vm?.Dest.NavigateCommand.Execute(RowUnder(e));

    // ---------------- Menu de contexto (DESTINATION) ----------------
    private FileRow? _dstCtxRow;

    private void GridDst_ContextRequested(object? sender, ContextRequestedEventArgs e)
        => _dstCtxRow = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<DataGridRow>().FirstOrDefault()?.DataContext as FileRow;

    private void CtxRename_Click(object? sender, RoutedEventArgs e) => Vm?.RenameDestItemCommand.Execute(_dstCtxRow);
    private void CtxDelete_Click(object? sender, RoutedEventArgs e) => Vm?.DeleteDestItemCommand.Execute(_dstCtxRow);
    private void CtxCopyPath_Click(object? sender, RoutedEventArgs e) => Vm?.CopyDestPathCommand.Execute(_dstCtxRow);

    private void Jobs_DoubleTapped(object? sender, TappedEventArgs e)
        => Vm?.JobActivateCommand.Execute(null);

    // Painéis SOURCE/DESTINATION são navegadores por duplo-clique: sem seleção persistente.
    private void Grid_SelectionCleared(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid g && g.SelectedIndex != -1) g.SelectedIndex = -1;
    }

    // ---------------- Teclado (F4/F5) ----------------
    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm != null && Vm.HandleKey(e.Key.ToString())) e.Handled = true;
    }
}
