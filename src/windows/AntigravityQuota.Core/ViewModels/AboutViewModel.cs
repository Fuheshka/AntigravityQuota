using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core.ViewModels;

public class AboutViewModel : INotifyPropertyChanged
{
    private readonly LocalizationManager _loc;
    private readonly Action<string>? _clipboardSetter;
    private readonly Action<string>? _urlOpener;
    private readonly Func<ServerEndpoint?>? _endpointResolver;
    private readonly Func<QuotaSnapshot?>? _snapshotResolver;

    private ServerEndpoint? _currentEndpoint;
    private QuotaSnapshot? _currentSnapshot;
    private DateTimeOffset? _lastUpdate;

    private bool _isConnected;
    private string _connectionStatusText = string.Empty;
    private string _serverPidText = "—";
    private string _serverPortsText = "—";
    private string _maskedCsrfText = "—";
    private string _lastSyncText = string.Empty;
    private string _copyStatusFeedback = string.Empty;
    private bool _hasCopyFeedback;
    private string _diagnosticReportText = string.Empty;
    private readonly string _appVersion;

    public string AppVersion => _appVersion;
    public string PlatformInfo => ".NET 9 • Windows x64";
    public string AuthorUrl => "https://github.com/Fuheshka";
    public string RepositoryUrl => "https://github.com/Fuheshka/AntigravityQuota";
    public string ReleasesUrl => "https://github.com/Fuheshka/AntigravityQuota/releases";

    public LocalizationManager Loc => _loc;

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetField(ref _isConnected, value);
    }

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        private set => SetField(ref _connectionStatusText, value);
    }

    public string ServerPidText
    {
        get => _serverPidText;
        private set => SetField(ref _serverPidText, value);
    }

    public string ServerPortsText
    {
        get => _serverPortsText;
        private set => SetField(ref _serverPortsText, value);
    }

    public string MaskedCsrfText
    {
        get => _maskedCsrfText;
        private set => SetField(ref _maskedCsrfText, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetField(ref _lastSyncText, value);
    }

    public string AuthorshipText => _loc.AboutAuthor;
    public string CopyReportButtonText => _loc.AboutCopyReport;

    public string CopyStatusFeedback
    {
        get => _copyStatusFeedback;
        private set
        {
            if (SetField(ref _copyStatusFeedback, value))
            {
                HasCopyFeedback = !string.IsNullOrEmpty(value);
            }
        }
    }

    public bool HasCopyFeedback
    {
        get => _hasCopyFeedback;
        private set => SetField(ref _hasCopyFeedback, value);
    }

    public string DiagnosticReportText
    {
        get => _diagnosticReportText;
        private set => SetField(ref _diagnosticReportText, value);
    }

    public ICommand CopyReportCommand { get; }
    public ICommand OpenGitHubCommand { get; }
    public ICommand OpenReleasesCommand { get; }
    public ICommand OpenAuthorCommand { get; }
    public ICommand RefreshCommand { get; }

    public AboutViewModel(
        LocalizationManager? loc = null,
        Action<string>? clipboardSetter = null,
        Action<string>? urlOpener = null,
        Func<ServerEndpoint?>? endpointResolver = null,
        Func<QuotaSnapshot?>? snapshotResolver = null,
        string? appVersion = null)
    {
        _loc = loc ?? LocalizationManager.Instance;
        _clipboardSetter = clipboardSetter;
        _urlOpener = urlOpener;
        _endpointResolver = endpointResolver;
        _snapshotResolver = snapshotResolver;
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? UpdateChecker.ResolveCurrentVersion() : appVersion;

        CopyReportCommand = new RelayCommand(CopyReport);
        OpenGitHubCommand = new RelayCommand(() => OpenUrl(RepositoryUrl));
        OpenReleasesCommand = new RelayCommand(() => OpenUrl(ReleasesUrl));
        OpenAuthorCommand = new RelayCommand(() => OpenUrl(AuthorUrl));
        RefreshCommand = new RelayCommand(RefreshState);

        ResetToOfflineState();
        UpdateReportText();
    }

    public void UpdateState(ServerEndpoint? endpoint, QuotaSnapshot? snapshot, DateTimeOffset? lastUpdate = null)
    {
        _currentEndpoint = endpoint;
        _currentSnapshot = snapshot;
        if (lastUpdate.HasValue)
        {
            _lastUpdate = lastUpdate.Value;
        }

        if (endpoint != null)
        {
            IsConnected = true;
            ConnectionStatusText = _loc.AboutConnected;
            ServerPidText = endpoint.Pid.ToString();
            ServerPortsText = (endpoint.Ports != null && endpoint.Ports.Count > 0)
                ? string.Join(", ", endpoint.Ports)
                : "—";
            MaskedCsrfText = DiagnosticReportGenerator.MaskCsrfToken(endpoint.CsrfToken);
        }
        else
        {
            ResetToOfflineState();
        }

        if (_lastUpdate.HasValue)
        {
            LastSyncText = _lastUpdate.Value.ToLocalTime().ToString("HH:mm:ss");
        }
        else
        {
            LastSyncText = _loc.AboutNever;
        }

        UpdateReportText();
    }

    private void ResetToOfflineState()
    {
        IsConnected = false;
        ConnectionStatusText = _loc.AboutDisconnected;
        ServerPidText = "—";
        ServerPortsText = "—";
        MaskedCsrfText = "—";
        LastSyncText = _lastUpdate.HasValue ? _lastUpdate.Value.ToLocalTime().ToString("HH:mm:ss") : _loc.AboutNever;
    }

    private void UpdateReportText()
    {
        DiagnosticReportText = DiagnosticReportGenerator.GenerateReport(
            endpoint: _currentEndpoint,
            snapshot: _currentSnapshot,
            appVersion: AppVersion,
            lastUpdate: _lastUpdate);
    }

    public void RefreshState()
    {
        var endpoint = _endpointResolver?.Invoke();
        var snapshot = _snapshotResolver?.Invoke();
        UpdateState(endpoint, snapshot, _lastUpdate);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(AuthorshipText));
        OnPropertyChanged(nameof(CopyReportButtonText));
        OnPropertyChanged(nameof(Loc));

        if (IsConnected)
        {
            ConnectionStatusText = _loc.AboutConnected;
        }
        else
        {
            ConnectionStatusText = _loc.AboutDisconnected;
        }

        if (!_lastUpdate.HasValue)
        {
            LastSyncText = _loc.AboutNever;
        }

        if (HasCopyFeedback)
        {
            CopyStatusFeedback = _loc.AboutReportCopied;
        }
    }

    private void CopyReport()
    {
        UpdateReportText();

        if (_clipboardSetter != null)
        {
            _clipboardSetter(DiagnosticReportText);
        }

        CopyStatusFeedback = _loc.AboutReportCopied;
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (_urlOpener != null)
        {
            _urlOpener(url);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fail gracefully if default browser fails to open
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
