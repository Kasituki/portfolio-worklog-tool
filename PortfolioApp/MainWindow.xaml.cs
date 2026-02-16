// MainWindow - UIイベント処理とOxyPlot更新に集中
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using PortfolioApp.Models;
using PortfolioApp.Services;

namespace PortfolioApp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly WorkLogRepository _repository;
        private readonly WorkLogImportService _importService;
        private readonly CsvExportService _exportService;

        // INotifyPropertyChanged 実装
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 取込結果サマリ用プロパティ
        private Visibility _importSummaryVisibility = Visibility.Collapsed;
        public Visibility ImportSummaryVisibility
        {
            get => _importSummaryVisibility;
            set { _importSummaryVisibility = value; OnPropertyChanged(); }
        }

        private string _importDateTime = "";
        public string ImportDateTime
        {
            get => _importDateTime;
            set { _importDateTime = value; OnPropertyChanged(); }
        }

        private string _importFileName = "";
        public string ImportFileName
        {
            get => _importFileName;
            set { _importFileName = value; OnPropertyChanged(); }
        }

        private string _totalReadCount = "";
        public string TotalReadCount
        {
            get => _totalReadCount;
            set { _totalReadCount = value; OnPropertyChanged(); }
        }

        private string _insertedCount = "";
        public string InsertedCount
        {
            get => _insertedCount;
            set { _insertedCount = value; OnPropertyChanged(); }
        }

        private string _skipDetails = "";
        public string SkipDetails
        {
            get => _skipDetails;
            set { _skipDetails = value; OnPropertyChanged(); }
        }

        private string _errorLogPath = "";
        public string ErrorLogPath
        {
            get => _errorLogPath;
            set
            {
                _errorLogPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasErrorLog));
            }
        }

        private bool _isDryRun = false;
        public bool IsDryRun
        {
            get => _isDryRun;
            set { _isDryRun = value; OnPropertyChanged(); }
        }

        private string _importMode = "";
        public string ImportMode
        {
            get => _importMode;
            set { _importMode = value; OnPropertyChanged(); }
        }

        private string _plannedInsertCount = "";
        public string PlannedInsertCount
        {
            get => _plannedInsertCount;
            set { _plannedInsertCount = value; OnPropertyChanged(); }
        }

        // 共通期間プロパティ
        private DateTime? _commonFromDate;
        public DateTime? CommonFromDate
        {
            get => _commonFromDate;
            set 
            { 
                _commonFromDate = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPeriodSet));
            }
        }

        private DateTime? _commonToDate;
        public DateTime? CommonToDate
        {
            get => _commonToDate;
            set 
            { 
                _commonToDate = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPeriodSet));
            }
        }

        // 期間が設定されているか（取得ボタンの有効化判定用）
        public bool IsPeriodSet => CommonFromDate.HasValue && CommonToDate.HasValue;

        // 取得ボタンが有効かどうか（期間設定済み かつ 処理中でない）
        public bool CanGetData => IsPeriodSet && IsNotLoading;

        // 期間更新状態プロパティ
        private string _periodUpdateStatus = "";
        public string PeriodUpdateStatus
        {
            get => _periodUpdateStatus;
            set { _periodUpdateStatus = value; OnPropertyChanged(); }
        }

        private Visibility _periodUpdateStatusVisible = Visibility.Collapsed;
        public Visibility PeriodUpdateStatusVisible
        {
            get => _periodUpdateStatusVisible;
            set { _periodUpdateStatusVisible = value; OnPropertyChanged(); }
        }

        // データ取得済みフラグ（CSV出力ボタン制御用・タブごと）
        private bool _isMonthlyDataLoaded = false;
        public bool IsMonthlyDataLoaded
        {
            get => _isMonthlyDataLoaded;
            set { _isMonthlyDataLoaded = value; OnPropertyChanged(); }
        }

        private bool _isProjectDataLoaded = false;
        public bool IsProjectDataLoaded
        {
            get => _isProjectDataLoaded;
            set { _isProjectDataLoaded = value; OnPropertyChanged(); }
        }

        private bool _isMemberDataLoaded = false;
        public bool IsMemberDataLoaded
        {
            get => _isMemberDataLoaded;
            set { _isMemberDataLoaded = value; OnPropertyChanged(); }
        }

        // 処理中フラグ（二重実行防止用）
        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                _isLoading = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotLoading));
                OnPropertyChanged(nameof(CanGetData));
            }
        }

        public bool IsNotLoading => !IsLoading;

        private readonly ObservableCollection<MonthlyHoursRow> _monthlyRows = new();
        private readonly ObservableCollection<ProjectHoursRow> _projectRows = new();
        private readonly ObservableCollection<MemberHoursRow> _memberRows = new();

        public bool HasErrorLog => !string.IsNullOrWhiteSpace(ErrorLogPath) && ErrorLogPath != "なし";

        public MainWindow()
        {
            InitializeComponent();

            // DataContext を自身に設定
            DataContext = this;

            // appsettings.json から接続文字列を読み込み
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var connectionString = configuration.GetConnectionString("PortfolioDb")
                ?? throw new InvalidOperationException("接続文字列 'PortfolioDb' が appsettings.json に見つかりません。");

            // サービスを初期化
            _repository = new WorkLogRepository(connectionString);
            _importService = new WorkLogImportService(_repository);
            _exportService = new CsvExportService();

            GridMonthly.ItemsSource = _monthlyRows;
            GridProject.ItemsSource = _projectRows;
            GridMember.ItemsSource = _memberRows;

            // 初期状態で空状態を表示
            UpdateEmptyState("Monthly", 0);
            UpdateEmptyState("Project", 0);
            UpdateEmptyState("Member", 0);

            // 期間更新状態の初期化
            MarkPeriodAsUnupdated();
        }

        /// <summary>
        /// 期間を「未更新」状態にマーク
        /// </summary>
        private void MarkPeriodAsUnupdated()
        {
            PeriodUpdateStatus = (string)FindResource("PeriodUnupdatedText");
            PeriodUpdateStatusVisible = Visibility.Visible;
            UpdateStatusIndicator.Fill = (System.Windows.Media.Brush)FindResource("UnupdatedBrush");
            UpdateStatusText.Foreground = (System.Windows.Media.Brush)FindResource("UnupdatedBrush");
            
            // 期間変更時は全タブのCSV出力を無効化
            IsMonthlyDataLoaded = false;
            IsProjectDataLoaded = false;
            IsMemberDataLoaded = false;

            // 全タブの表示結果をクリア
            ClearAllTabsData();
        }

        /// <summary>
        /// 全タブのデータと表示をクリア
        /// </summary>
        private void ClearAllTabsData()
        {
            // 月別工数タブをクリア
            _monthlyRows.Clear();
            ChartMonthly.Model = null;
            UpdateEmptyState("Monthly", 0);

            // 案件別工数タブをクリア
            _projectRows.Clear();
            ChartProject.Model = null;
            UpdateEmptyState("Project", 0);

            // メンバー別工数タブをクリア
            _memberRows.Clear();
            ChartMember.Model = null;
            UpdateEmptyState("Member", 0);
        }

        /// <summary>
        /// 期間を「更新済」状態にマーク
        /// </summary>
        private void MarkPeriodAsUpdated()
        {
            var now = DateTime.Now.ToString("HH:mm:ss");
            var prefix = (string)FindResource("PeriodUpdatedPrefix");
            var suffix = (string)FindResource("PeriodUpdatedSuffix");
            PeriodUpdateStatus = $"{prefix}{now}{suffix}";
            PeriodUpdateStatusVisible = Visibility.Visible;
            UpdateStatusIndicator.Fill = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            UpdateStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }

        /// <summary>
        /// 期間バリデーション
        /// </summary>
        /// <returns>true: 有効, false: 無効</returns>
        private bool ValidatePeriod(out string errorMessage)
        {
            errorMessage = "";

            // From/To 未入力チェック
            if (!CommonFromDate.HasValue || !CommonToDate.HasValue)
            {
                errorMessage = "期間（From/To）を入力してください。";
                return false;
            }

            // From > To チェック
            if (CommonFromDate.Value > CommonToDate.Value)
            {
                errorMessage = "期間の指定が正しくありません。\nFrom日付がTo日付より後になっています。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 期間エラー表示（ダイアログボックス）
        /// </summary>
        private void ShowPeriodError(string errorMessage)
        {
            MessageBox.Show(errorMessage, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 期間DatePicker変更イベント
        /// </summary>
        private void CommonDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            MarkPeriodAsUnupdated();
        }

        // 取込結果サマリを更新
        private void UpdateImportSummary(string csvFilePath, ImportResult result, bool isDryRun)
        {
            ImportDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            ImportMode = isDryRun ? "ドライラン" : "通常";
            ImportFileName = Path.GetFileName(csvFilePath);
            TotalReadCount = $"{result.TotalRead} 件";
            PlannedInsertCount = $"{result.PlannedInsert} 件";
            InsertedCount = isDryRun ? "0 件（投入なし）" : $"{result.Inserted} 件";

            if (result.SkippedRecords.Count > 0)
            {
                SkipDetails = GetSkipCounts(result);
            }
            else
            {
                SkipDetails = "なし";
            }

            ErrorLogPath = string.IsNullOrEmpty(result.ErrorLogPath) ? "なし" : result.ErrorLogPath;
            ImportSummaryVisibility = Visibility.Visible;
        }

        // スキップ内訳取得
        private string GetSkipCounts(ImportResult result)
        {
            var lines = new System.Collections.Generic.List<string>();

            if (result.RequiredMissing > 0)
                lines.Add($" - 必須項目欠損: {result.RequiredMissing}");
            if (result.InvalidWorkDate > 0)
                lines.Add($" - 日付型変換エラー: {result.InvalidWorkDate}");
            if (result.InvalidHours > 0)
                lines.Add($" - 工数型変換エラー: {result.InvalidHours}");
            if (result.DuplicateInFile > 0)
                lines.Add($" - ファイル内重複: {result.DuplicateInFile}");
            if (result.DuplicateInDb > 0)
                lines.Add($" - DB重複: {result.DuplicateInDb}");

            return string.Join("\n", lines);
        }

        private async void BtnImportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
                    Title = "工数ログCSVを選択"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var csvFilePath = dialog.FileName;

                // CSV取込サービスを呼び出し
                var result = await _importService.ImportAsync(csvFilePath, IsDryRun);

                // サマリ更新
                UpdateImportSummary(csvFilePath, result, IsDryRun);

                // 完了メッセージ
                var skipCounts = GetSkipCounts(result);
                var modeText = IsDryRun ? "【ドライラン】" : "";
                var completionMessage = $"{modeText}CSV取込が完了しました。\n\n読込件数: {result.TotalRead}\n投入予定件数: {result.PlannedInsert}\n実投入件数: {result.Inserted}\nスキップ: {result.SkippedRecords.Count}";

                if (result.SkippedRecords.Count > 0)
                {
                    completionMessage += $"\n\n内訳:\n{skipCounts}";
                }

                if (!string.IsNullOrEmpty(result.ErrorLogPath))
                {
                    completionMessage += $"\n\nエラーレポート:\n{result.ErrorLogPath}";
                }

                MessageBox.Show(completionMessage, "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnGetMonthly_Click(object sender, RoutedEventArgs e)
        {
            // 期間バリデーション
            if (!ValidatePeriod(out string errorMessage))
            {
                ShowPeriodError(errorMessage);
                return;
            }

            // 二重実行防止
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;

                // 共通期間を取得
                DateTime fromDate = CommonFromDate!.Value;
                DateTime toDate = CommonToDate!.Value.AddDays(1);

                _monthlyRows.Clear();
                var rows = await _repository.GetMonthlyAsync(fromDate, toDate);

                foreach (var row in rows)
                {
                    _monthlyRows.Add(row);
                }

                UpdateMonthlyChart();
                UpdateEmptyState("Monthly", _monthlyRows.Count);
                MarkPeriodAsUpdated();
                IsMonthlyDataLoaded = true;

                // 取得結果が0件の場合のメッセージ
                if (_monthlyRows.Count == 0)
                {
                    MessageBox.Show($"指定期間にデータがありませんでした。\n\n期間: {fromDate:yyyy/MM/dd} 〜 {CommonToDate!.Value:yyyy/MM/dd}",
                        "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                // DB接続エラー時のメッセージ
                MessageBox.Show($"データベース接続エラーが発生しました。\n\n詳細: {ex.Message}\n\n接続先を確認してください。",
                    "データベースエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void BtnGetProject_Click(object sender, RoutedEventArgs e)
        {
            // 期間バリデーション
            if (!ValidatePeriod(out string errorMessage))
            {
                ShowPeriodError(errorMessage);
                return;
            }

            // 二重実行防止
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;

                // 共通期間を取得
                DateTime fromDate = CommonFromDate!.Value;
                DateTime toDate = CommonToDate!.Value.AddDays(1);

                _projectRows.Clear();
                var rows = await _repository.GetProjectTopAsync(fromDate, toDate);

                foreach (var row in rows)
                {
                    _projectRows.Add(row);
                }

                UpdateProjectChart();
                UpdateEmptyState("Project", _projectRows.Count);
                MarkPeriodAsUpdated();
                IsProjectDataLoaded = true;

                // 取得結果が0件の場合のメッセージ
                if (_projectRows.Count == 0)
                {
                    MessageBox.Show($"指定期間にデータがありませんでした。\n\n期間: {fromDate:yyyy/MM/dd} 〜 {CommonToDate!.Value:yyyy/MM/dd}",
                        "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                // DB接続エラー時のメッセージ
                MessageBox.Show($"データベース接続エラーが発生しました。\n\n詳細: {ex.Message}\n\n接続先を確認してください。",
                    "データベースエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void BtnGetMember_Click(object sender, RoutedEventArgs e)
        {
            // 期間バリデーション
            if (!ValidatePeriod(out string errorMessage))
            {
                ShowPeriodError(errorMessage);
                return;
            }

            // 二重実行防止
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;

                // 共通期間を取得
                DateTime fromDate = CommonFromDate!.Value;
                DateTime toDate = CommonToDate!.Value.AddDays(1);

                _memberRows.Clear();
                var rows = await _repository.GetMemberTopAsync(fromDate, toDate);

                foreach (var row in rows)
                {
                    _memberRows.Add(row);
                }

                UpdateMemberChart();
                UpdateEmptyState("Member", _memberRows.Count);
                MarkPeriodAsUpdated();
                IsMemberDataLoaded = true;

                // 取得結果が0件の場合のメッセージ
                if (_memberRows.Count == 0)
                {
                    MessageBox.Show($"指定期間にデータがありませんでした。\n\n期間: {fromDate:yyyy/MM/dd} 〜 {CommonToDate!.Value:yyyy/MM/dd}",
                        "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                // DB接続エラー時のメッセージ
                MessageBox.Show($"データベース接続エラーが発生しました。\n\n詳細: {ex.Message}\n\n接続先を確認してください。",
                    "データベースエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BtnExportMonthlyCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_monthlyRows.Count == 0)
                {
                    MessageBox.Show("出力するデータがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "CSVファイル (*.csv)|*.csv",
                    FileName = $"月別工数_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    _exportService.ExportMonthly(_monthlyRows, dialog.FileName);
                    MessageBox.Show("CSV出力が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportProjectCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_projectRows.Count == 0)
                {
                    MessageBox.Show("出力するデータがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "CSVファイル (*.csv)|*.csv",
                    FileName = $"案件別工数_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    _exportService.ExportProject(_projectRows, dialog.FileName);
                    MessageBox.Show("CSV出力が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportMemberCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_memberRows.Count == 0)
                {
                    MessageBox.Show("出力するデータがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "CSVファイル (*.csv)|*.csv",
                    FileName = $"メンバー別工数_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    _exportService.ExportMember(_memberRows, dialog.FileName);
                    MessageBox.Show("CSV出力が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateMonthlyChart()
        {
            var plot = new PlotModel { Title = "月別工数" };
            
            // StemSeriesを使用して棒グラフ風の表示にする
            var stemSeries = new StemSeries 
            { 
                Title = "TotalHours",
                Color = OxyColor.FromRgb(33, 150, 243), // 青系の色
                StrokeThickness = 50, // 棒を太く見せる（データが少ない場合でも見栄えする）
                MarkerType = MarkerType.None
            };

            for (var i = 0; i < _monthlyRows.Count; i++)
            {
                var row = _monthlyRows[i];
                stemSeries.Points.Add(new DataPoint(i, (double)row.TotalHours));
            }

            plot.Series.Add(stemSeries);
            
            // カテゴリ軸（横軸）の設定
            var categoryAxis = new CategoryAxis 
            { 
                Position = AxisPosition.Bottom,
                GapWidth = 0.3 // 棒の間隔を狭めて太く見せる（デフォルト1.0）
            };
            categoryAxis.Labels.AddRange(_monthlyRows.Select(r => r.Month));
            plot.Axes.Add(categoryAxis);
            
            // 数値軸（縦軸）の設定
            plot.Axes.Add(new LinearAxis 
            { 
                Position = AxisPosition.Left, 
                Title = "Hours",
                MinimumPadding = 0.05,
                MaximumPadding = 0.1,
                Minimum = 0
            });

            ChartMonthly.Model = plot;
        }

        private void UpdateProjectChart()
        {
            var plot = new PlotModel { Title = "案件別工数（グラフはTOP10）" };
            var barSeries = new BarSeries { Title = "TotalHours" };

            // グラフは上位10件のみ表示
            var top10 = _projectRows.Take(10).ToList();
            
            for (var i = 0; i < top10.Count; i++)
            {
                var row = top10[i];
                barSeries.Items.Add(new BarItem((double)row.TotalHours));
            }

            plot.Series.Add(barSeries);
            var categoryAxis = new CategoryAxis { Position = AxisPosition.Left };
            categoryAxis.Labels.AddRange(top10.Select(r => r.Project));
            plot.Axes.Add(categoryAxis);
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Hours" });

            ChartProject.Model = plot;
        }

        private void UpdateMemberChart()
        {
            var plot = new PlotModel { Title = "メンバー別工数（グラフはTOP10）" };
            var barSeries = new BarSeries { Title = "TotalHours" };

            // グラフは上位10件のみ表示
            var top10 = _memberRows.Take(10).ToList();
            
            for (var i = 0; i < top10.Count; i++)
            {
                var row = top10[i];
                barSeries.Items.Add(new BarItem((double)row.TotalHours));
            }

            plot.Series.Add(barSeries);
            var categoryAxis = new CategoryAxis { Position = AxisPosition.Left };
            categoryAxis.Labels.AddRange(top10.Select(r => r.Member));
            plot.Axes.Add(categoryAxis);
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Hours" });

            ChartMember.Model = plot;
        }

        /// <summary>
        /// 空状態の表示/非表示を更新（グラフの折りたたみも制御）
        /// </summary>
        private void UpdateEmptyState(string tabName, int rowCount)
        {
            var isEmpty = rowCount == 0;

            switch (tabName)
            {
                case "Monthly":
                    EmptyStateMonthly.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
                    GridMonthly.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    // 空状態時はグラフを折りたたむ
                    ChartContainerMonthly.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case "Project":
                    EmptyStateProject.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
                    GridProject.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    // 空状態時はグラフを折りたたむ
                    ChartContainerProject.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case "Member":
                    EmptyStateMember.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
                    GridMember.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    // 空状態時はグラフを折りたたむ
                    ChartContainerMember.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
                    break;
            }
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = ErrorLogPath;

                if (string.IsNullOrWhiteSpace(path) || path == "なし")
                {
                    MessageBox.Show("エラーログはありません。", "情報",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!File.Exists(path))
                {
                    MessageBox.Show($"ログファイルが見つかりません。\n{path}", "情報",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 共通期間の「全期間」ボタン処理
        private async void BtnSetCommonDateRange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (minDate, maxDate) = await _repository.GetWorkDateRangeAsync();

                if (minDate.HasValue && maxDate.HasValue)
                {
                    CommonFromDate = minDate.Value;
                    CommonToDate = maxDate.Value;
                    MarkPeriodAsUnupdated(); // 期間が変更されたので未更新状態に
                    MessageBox.Show($"期間を設定しました。\n\nFrom: {CommonFromDate:yyyy/MM/dd}\nTo: {CommonToDate:yyyy/MM/dd}",
                        "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("dbo.WorkLogs にデータが存在しません。", "情報",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
