﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using System.Windows.Threading;

using EpgTimer.EpgView;

namespace EpgTimer
{
    /// <summary>
    /// EpgMainView.xaml の相互作用ロジック
    /// </summary>
    public partial class EpgMainView : UserControl
    {
        public event Action<object, CustomEpgTabInfo, object> ViewModeChangeRequested;
        private object scrollToTarget;

        private CustomEpgTabInfo setViewInfo = null;

        private List<EpgServiceInfo> serviceList = new List<EpgServiceInfo>();
        private SortedList<DateTime, List<ProgramViewItem>> timeList = new SortedList<DateTime, List<ProgramViewItem>>();
        private List<ReserveViewItem> reserveList = new List<ReserveViewItem>();
        private Point clickPos;
        private DispatcherTimer nowViewTimer;
        private Line nowLine = null;

        private bool updateEpgData = true;
        private bool updateReserveData = true;

        public EpgMainView(CustomEpgTabInfo setInfo)
        {
            InitializeComponent();

            nowViewTimer = new DispatcherTimer(DispatcherPriority.Normal);
            nowViewTimer.Tick += new EventHandler(WaitReDrawNowLine);
            setViewInfo = setInfo;
        }

        /// <summary>
        /// 保持情報のクリア
        /// </summary>
        public void ClearInfo()
        {
            nowViewTimer.Stop();
            if (nowLine != null)
            {
                epgProgramView.canvas.Children.Remove(nowLine);
            }
            nowLine = null;

            epgProgramView.ClearInfo();
            timeView.ClearInfo();
            serviceView.ClearInfo();
            dateView.ClearInfo();
            timeList.Clear();
            serviceList.Clear();
            reserveList.Clear();
        }

        public bool HasService(ushort onid, ushort tsid, ushort sid)
        {
            return setViewInfo.ViewServiceList.Contains(CommonManager.Create64Key(onid, tsid, sid));
        }

        /// <summary>
        /// 現在ライン表示用タイマーイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WaitReDrawNowLine(object sender, EventArgs e)
        {
            ReDrawNowLine();
        }

        /// <summary>
        /// 現在ライン表示
        /// </summary>
        private void ReDrawNowLine()
        {
            try
            {
                nowViewTimer.Stop();
                DateTime nowTime = DateTime.UtcNow.AddHours(9);
                if (timeList.Count < 1 || nowTime < timeList.Keys[0])
                {
                    if (nowLine != null)
                    {
                        epgProgramView.canvas.Children.Remove(nowLine);
                    }
                    nowLine = null;
                    return;
                }
                if (nowLine == null)
                {
                    nowLine = new Line();
                    Canvas.SetZIndex(nowLine, 20);
                    nowLine.Stroke = new SolidColorBrush(Colors.Red);
                    nowLine.StrokeThickness = 3;
                    nowLine.Opacity = 0.7;
                    nowLine.Effect = new System.Windows.Media.Effects.DropShadowEffect() { BlurRadius = 10 };
                    nowLine.IsHitTestVisible = false;
                    epgProgramView.canvas.Children.Add(nowLine);
                }

                double posY = 0;
                DateTime chkNowTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, 0, 0);
                for (int i = 0; i < timeList.Count; i++)
                {
                    if (chkNowTime == timeList.Keys[i])
                    {
                        posY = Math.Ceiling((i * 60 + (nowTime - chkNowTime).TotalMinutes) * Settings.Instance.MinHeight);
                        break;
                    }
                    else if (chkNowTime < timeList.Keys[i])
                    {
                        //時間省かれてる
                        posY = Math.Ceiling(i * 60 * Settings.Instance.MinHeight);
                        break;
                    }
                }

                if (posY > epgProgramView.canvas.Height)
                {
                    if (nowLine != null)
                    {
                        epgProgramView.canvas.Children.Remove(nowLine);
                    }
                    nowLine = null;
                    return;
                }

                nowLine.X1 = 0;
                nowLine.Y1 = posY;
                nowLine.X2 = epgProgramView.canvas.Width;
                nowLine.Y2 = posY;

                nowViewTimer.Interval = TimeSpan.FromSeconds(60 - nowTime.Second);
                nowViewTimer.Start();
            }
            catch
            {
            }
        }

        /// <summary>
        /// 表示スクロールイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void epgProgramView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                if (sender.GetType() == typeof(ProgramView))
                {
                    //時間軸の表示もスクロール
                    timeView.scrollViewer.ScrollToVerticalOffset(epgProgramView.scrollViewer.VerticalOffset);
                    //サービス名表示もスクロール
                    serviceView.scrollViewer.ScrollToHorizontalOffset(epgProgramView.scrollViewer.HorizontalOffset);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// マウスホイールイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void epgProgramView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                e.Handled = true;
                if (sender.GetType() == typeof(ProgramView))
                {
                    ProgramView view = sender as ProgramView;
                    if (Settings.Instance.MouseScrollAuto == true)
                    {
                        view.scrollViewer.ScrollToVerticalOffset(view.scrollViewer.VerticalOffset - e.Delta);
                    }
                    else
                    {
                        if (e.Delta < 0)
                        {
                            //下方向
                            view.scrollViewer.ScrollToVerticalOffset(view.scrollViewer.VerticalOffset + Settings.Instance.ScrollSize);
                        }
                        else
                        {
                            //上方向
                            if (view.scrollViewer.VerticalOffset < Settings.Instance.ScrollSize)
                            {
                                view.scrollViewer.ScrollToVerticalOffset(0);
                            }
                            else
                            {
                                view.scrollViewer.ScrollToVerticalOffset(view.scrollViewer.VerticalOffset - Settings.Instance.ScrollSize);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// マウス位置から予約情報を取得する
        /// </summary>
        /// <param name="cursorPos">[IN]マウス位置</param>
        /// <param name="reserve">[OUT]予約情報</param>
        /// <returns>falseで存在しない</returns>
        private bool GetReserveItem(Point cursorPos, ref ReserveData reserve)
        {
            try
            {
                {
                    foreach (ReserveViewItem resInfo in reserveList)
                    {
                        if (resInfo.LeftPos <= cursorPos.X && cursorPos.X < resInfo.LeftPos + resInfo.Width &&
                            resInfo.TopPos <= cursorPos.Y && cursorPos.Y < resInfo.TopPos + resInfo.Height)
                        {
                            reserve = resInfo.ReserveInfo;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
            return false;
        }

        /// <summary>
        /// マウス位置から番組情報を取得する
        /// </summary>
        /// <param name="cursorPos">[IN]マウス位置</param>
        /// <returns>nullで存在しない</returns>
        private ProgramViewItem GetProgramItem(Point cursorPos)
        {
            {
                int timeIndex = (int)(cursorPos.Y / (60 * Settings.Instance.MinHeight));
                if (0 <= timeIndex && timeIndex < timeList.Count)
                {
                    foreach (ProgramViewItem pgInfo in timeList.Values[timeIndex])
                    {
                        if (pgInfo.LeftPos <= cursorPos.X && cursorPos.X < pgInfo.LeftPos + pgInfo.Width &&
                            pgInfo.TopPos <= cursorPos.Y && cursorPos.Y < pgInfo.TopPos + pgInfo.Height)
                        {
                            return pgInfo;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 左ボタンダブルクリックイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cursorPos"></param>
        void epgProgramView_LeftDoubleClick(object sender, Point cursorPos)
        {
            try
            {
                //まず予約情報あるかチェック
                ReserveData reserve = new ReserveData();
                if (GetReserveItem(cursorPos, ref reserve) == true)
                {
                    //予約変更ダイアログ表示
                    ChangeReserve(reserve);
                    return;
                }
                //番組情報あるかチェック
                ProgramViewItem program = GetProgramItem(cursorPos);
                if (program != null)
                {
                    //予約追加ダイアログ表示
                    AddReserve(program.EventInfo, program.Past == false);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右ボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cursorPos"></param>
        void epgProgramView_RightClick(object sender, Point cursorPos)
        {
            try
            {
                //右クリック表示メニューの作成
                clickPos = cursorPos;
                ReserveData reserve = new ReserveData();
                ProgramViewItem program = null;
                bool addMode = false;
                if (GetReserveItem(cursorPos, ref reserve) == false)
                {
                    program = GetProgramItem(cursorPos);
                    addMode = true;
                }
                ContextMenu menu = new ContextMenu();

                MenuItem menuItemNew = new MenuItem();
                menuItemNew.Header = "簡易予約";
                menuItemNew.Tag = (uint)0;
                menuItemNew.Click += new RoutedEventHandler(cm_add_preset_Click);

                MenuItem menuItemAdd = new MenuItem();
                menuItemAdd.Header = "予約追加 (_C)";

                MenuItem menuItemAddDlg = new MenuItem();
                menuItemAddDlg.Header = "ダイアログ表示 (_X)";
                menuItemAddDlg.Click += new RoutedEventHandler(cm_add_Click);

                menuItemAdd.Items.Add(menuItemAddDlg);
                menuItemAdd.Items.Add(new Separator());

                foreach (RecPresetItem info in Settings.GetRecPresetList())
                {
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = info.DisplayName;
                    menuItem.Tag = info.ID;
                    menuItem.Click += new RoutedEventHandler(cm_add_preset_Click);
                    menuItem.IsEnabled = program != null && program.Past == false;
                    menuItemAdd.Items.Add(menuItem);
                }

                MenuItem menuItemChg = new MenuItem();
                menuItemChg.Header = "予約変更 (_C)";
                MenuItem menuItemChgDlg = new MenuItem();
                menuItemChgDlg.Header = "ダイアログ表示 (_X)";
                menuItemChgDlg.Click += new RoutedEventHandler(cm_chg_Click);

                menuItemChg.Items.Add(menuItemChgDlg);
                menuItemChg.Items.Add(new Separator());

                for (byte i = 0; i < CommonManager.Instance.RecModeList.Length; i++)
                {
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = CommonManager.Instance.RecModeList[i] + " (_" + i + ")";
                    menuItem.Tag = i;
                    menuItem.Click += new RoutedEventHandler(cm_chg_recmode_Click);
                    menuItem.IsChecked = i == reserve.RecSetting.RecMode;
                    menuItemChg.Items.Add(menuItem);
                }
                menuItemChg.Items.Add(new Separator());

                MenuItem menuItemChgRecPri = new MenuItem();
                menuItemChgRecPri.Header = "優先度 " + reserve.RecSetting.Priority + " (_E)";

                for (byte i = 1; i <= 5; i++)
                {
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = "_" + i;
                    menuItem.Tag = i;
                    menuItem.Click += new RoutedEventHandler(cm_chg_priority_Click);
                    menuItem.IsChecked = i == reserve.RecSetting.Priority;
                    menuItemChgRecPri.Items.Add(menuItem);
                }
                menuItemChg.Items.Add(menuItemChgRecPri);

                MenuItem menuItemDel = new MenuItem();
                menuItemDel.Header = "予約削除";
                menuItemDel.Click += new RoutedEventHandler(cm_del_Click);

                MenuItem menuItemAutoAdd = new MenuItem();
                menuItemAutoAdd.Header = "自動予約登録";
                menuItemAutoAdd.Click += new RoutedEventHandler(cm_autoadd_Click);
                MenuItem menuItemTimeshift = new MenuItem();
                menuItemTimeshift.Header = "追っかけ再生 (_P)";
                menuItemTimeshift.Click += new RoutedEventHandler(cm_timeShiftPlay_Click);

                //表示モード
                MenuItem menuItemView = new MenuItem();
                menuItemView.Header = "表示モード (_W)";

                MenuItem menuItemViewSetDlg = new MenuItem();
                menuItemViewSetDlg.Header = "表示設定";
                menuItemViewSetDlg.Click += new RoutedEventHandler(cm_viewSet_Click);

                MenuItem menuItemChgViewMode2 = new MenuItem();
                menuItemChgViewMode2.Header = "1週間モード (_2)";
                menuItemChgViewMode2.Tag = 1;
                menuItemChgViewMode2.Click += new RoutedEventHandler(cm_chg_viewMode_Click);
                MenuItem menuItemChgViewMode3 = new MenuItem();
                menuItemChgViewMode3.Header = "リスト表示モード (_3)";
                menuItemChgViewMode3.Tag = 2;
                menuItemChgViewMode3.Click += new RoutedEventHandler(cm_chg_viewMode_Click);

                menuItemView.Items.Add(menuItemChgViewMode2);
                menuItemView.Items.Add(menuItemChgViewMode3);
                menuItemView.Items.Add(new Separator());
                menuItemView.Items.Add(menuItemViewSetDlg);
                if (addMode && program == null)
                {
                    menuItemNew.IsEnabled = false;
                    menuItemAdd.IsEnabled = false;
                    menuItemChg.IsEnabled = false;
                    menuItemDel.IsEnabled = false;
                    menuItemAutoAdd.IsEnabled = false;
                    menuItemTimeshift.IsEnabled = false;
                    menuItemView.IsEnabled = true;
                }
                else
                {
                    if (addMode == false)
                    {
                        menuItemNew.IsEnabled = false;
                        menuItemAdd.IsEnabled = false;
                        menuItemChg.IsEnabled = true;
                        menuItemDel.IsEnabled = true;
                        menuItemAutoAdd.IsEnabled = true;
                        menuItemTimeshift.IsEnabled = true;
                        menuItemView.IsEnabled = true;
                    }
                    else
                    {
                        menuItemNew.IsEnabled = program.Past == false;
                        menuItemAdd.IsEnabled = true;
                        menuItemChg.IsEnabled = false;
                        menuItemDel.IsEnabled = false;
                        menuItemAutoAdd.IsEnabled = true;
                        menuItemTimeshift.IsEnabled = false;
                        menuItemView.IsEnabled = true;
                    }
                }

                menu.Items.Add(menuItemNew);
                menu.Items.Add(menuItemAdd);
                menu.Items.Add(menuItemChg);
                menu.Items.Add(menuItemDel);
                menu.Items.Add(menuItemAutoAdd);
                menu.Items.Add(menuItemTimeshift);
                menu.Items.Add(menuItemView);
                menu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー プリセットクリックイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void cm_add_preset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                uint presetID = (uint)((MenuItem)sender).Tag;

                ProgramViewItem program = GetProgramItem(clickPos);
                if (program == null)
                {
                    return;
                }
                EpgEventInfo eventInfo = program.EventInfo;
                if (eventInfo.StartTimeFlag == 0)
                {
                    MessageBox.Show("開始時間未定のため予約できません");
                    return;
                }

                ReserveData reserveInfo = new ReserveData();
                if (eventInfo.ShortInfo != null)
                {
                    reserveInfo.Title = eventInfo.ShortInfo.event_name;
                }

                reserveInfo.StartTime = eventInfo.start_time;
                reserveInfo.StartTimeEpg = eventInfo.start_time;

                if (eventInfo.DurationFlag == 0)
                {
                    reserveInfo.DurationSecond = 10 * 60;
                }
                else
                {
                    reserveInfo.DurationSecond = eventInfo.durationSec;
                }

                UInt64 key = CommonManager.Create64Key(eventInfo.original_network_id, eventInfo.transport_stream_id, eventInfo.service_id);
                if (ChSet5.Instance.ChList.ContainsKey(key) == true)
                {
                    reserveInfo.StationName = ChSet5.Instance.ChList[key].ServiceName;
                }
                reserveInfo.OriginalNetworkID = eventInfo.original_network_id;
                reserveInfo.TransportStreamID = eventInfo.transport_stream_id;
                reserveInfo.ServiceID = eventInfo.service_id;
                reserveInfo.EventID = eventInfo.event_id;

                RecSettingData setInfo = new RecSettingData();
                Settings.GetDefRecSetting(presetID, ref setInfo);
                reserveInfo.RecSetting = setInfo;

                List<ReserveData> list = new List<ReserveData>();
                list.Add(reserveInfo);
                ErrCode err = CommonManager.CreateSrvCtrl().SendAddReserve(list);
                if (err != ErrCode.CMD_SUCCESS)
                {
                    MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "予約登録でエラーが発生しました。終了時間がすでに過ぎている可能性があります。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 予約追加クリックイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgramViewItem program = GetProgramItem(clickPos);
                if (program == null)
                {
                    return;
                }
                AddReserve(program.EventInfo, program.Past == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 予約変更クリックイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_chg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReserveData reserve = new ReserveData();
                if (GetReserveItem(clickPos, ref reserve) == false)
                {
                    return;
                }
                ChangeReserve(reserve);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 予約削除クリックイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_del_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReserveData reserve = new ReserveData();
                if (GetReserveItem(clickPos, ref reserve) == false)
                {
                    return;
                }
                List<UInt32> list = new List<UInt32>();
                list.Add(reserve.ReserveID);
                ErrCode err = CommonManager.CreateSrvCtrl().SendDelReserve(list);
                if (err != ErrCode.CMD_SUCCESS)
                {
                    MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "予約削除でエラーが発生しました。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 予約モード変更イベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_chg_recmode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReserveData reserve = new ReserveData();
                if (GetReserveItem(clickPos, ref reserve) == false)
                {
                    return;
                }
                reserve.RecSetting.RecMode = (byte)((MenuItem)sender).Tag;
                List<ReserveData> list = new List<ReserveData>();
                list.Add(reserve);
                ErrCode err = CommonManager.CreateSrvCtrl().SendChgReserve(list);
                if (err != ErrCode.CMD_SUCCESS)
                {
                    MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "予約変更でエラーが発生しました。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 優先度変更イベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_chg_priority_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReserveData reserve = new ReserveData();
                if (GetReserveItem(clickPos, ref reserve) == false)
                {
                    return;
                }
                reserve.RecSetting.Priority = (byte)((MenuItem)sender).Tag;
                List<ReserveData> list = new List<ReserveData>();
                list.Add(reserve);
                ErrCode err = CommonManager.CreateSrvCtrl().SendChgReserve(list);
                if (err != ErrCode.CMD_SUCCESS)
                {
                    MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "予約変更でエラーが発生しました。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 自動予約登録イベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_autoadd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender.GetType() != typeof(MenuItem))
                {
                    return;
                }

                ProgramViewItem programView = GetProgramItem(clickPos);
                if (programView == null)
                {
                    return;
                }
                EpgEventInfo program = programView.EventInfo;

                SearchWindow dlg = new SearchWindow();
                dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
                dlg.SetViewMode(1);

                EpgSearchKeyInfo key = new EpgSearchKeyInfo();

                if (program.ShortInfo != null)
                {
                    key.andKey = program.ShortInfo.event_name;
                }
                Int64 sidKey = ((Int64)program.original_network_id) << 32 | ((Int64)program.transport_stream_id) << 16 | ((Int64)program.service_id);
                key.serviceList.Add(sidKey);

                dlg.SetSearchDefKey(key);
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 追っかけ再生イベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_timeShiftPlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender.GetType() != typeof(MenuItem))
                {
                    return;
                }

                ReserveData reserve = new ReserveData();
                if (GetReserveItem(clickPos, ref reserve) == false)
                {
                    return;
                }
                CommonManager.Instance.FilePlay(reserve.ReserveID);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 右クリックメニュー 表示設定イベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_viewSet_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.Instance.UseCustomEpgView == false)
            {
                MessageBox.Show("デフォルト表示では設定を変更することはできません。");
            }
            else
            {
                var dlg = new EpgDataViewSettingWindow();
                dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
                dlg.SetDefSetting(setViewInfo);
                if (dlg.ShowDialog() == true)
                {
                    var setInfo = new CustomEpgTabInfo();
                    dlg.GetSetting(ref setInfo);
                    if (setInfo.ViewMode == setViewInfo.ViewMode)
                    {
                        setViewInfo = setInfo;
                        UpdateEpgData();
                    }
                    else if (ViewModeChangeRequested != null)
                    {
                        ViewModeChangeRequested(this, setInfo, null);
                    }
                }
            }
        }

        /// <summary>
        /// 右クリックメニュー 表示モードイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cm_chg_viewMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModeChangeRequested != null)
                {
                    CustomEpgTabInfo setInfo = setViewInfo.DeepClone();
                    setInfo.ViewMode = (int)((MenuItem)sender).Tag;
                    ProgramViewItem program = GetProgramItem(clickPos);
                    ViewModeChangeRequested(this, setInfo, (program != null ? program.EventInfo : null));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 予約変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeReserve(ReserveData reserveInfo)
        {
            try
            {
                ChgReserveWindow dlg = new ChgReserveWindow();
                dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
                dlg.SetOpenMode(Settings.Instance.EpgInfoOpenMode);
                dlg.SetReserveInfo(reserveInfo);
                if (dlg.ShowDialog() == true)
                {
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 予約追加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddReserve(EpgEventInfo eventInfo, bool reservable)
        {
            try
            {
                AddReserveEpgWindow dlg = new AddReserveEpgWindow();
                dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
                dlg.SetOpenMode(Settings.Instance.EpgInfoOpenMode);
                dlg.SetReservable(reservable);
                dlg.SetEventInfo(eventInfo);
                if (dlg.ShowDialog() == true)
                {
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 表示位置変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void epgDateView_TimeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Button timeButton = sender as Button;

                DateTime time = (DateTime)timeButton.DataContext;
                if (timeList.Count < 1 || time < timeList.Keys[0])
                {
                    epgProgramView.scrollViewer.ScrollToVerticalOffset(0);
                }
                else
                {
                    for (int i = 0; i < timeList.Count; i++)
                    {
                        if (time <= timeList.Keys[i])
                        {
                            epgProgramView.scrollViewer.ScrollToVerticalOffset(Math.Ceiling(i * 60 * Settings.Instance.MinHeight));
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 現在ボタンクリックイベント呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_now_Click(object sender, RoutedEventArgs e)
        {
            MoveNowTime();
        }

        /// <summary>
        /// 表示位置を現在の時刻にスクロールする
        /// </summary>
        public void MoveNowTime()
        {
            try
            {
                if (timeList.Count <= 0)
                {
                    return;
                }
                DateTime startTime = timeList.Keys[0];

                DateTime time = DateTime.UtcNow.AddHours(9);
                if (time < startTime)
                {
                    epgProgramView.scrollViewer.ScrollToVerticalOffset(0);
                }
                else
                {
                    for (int i = 0; i < timeList.Count; i++)
                    {
                        if (time <= timeList.Keys[i])
                        {
                            double pos = ((i - 1) * 60 * Settings.Instance.MinHeight) - 100;
                            if (pos < 0)
                            {
                                pos = 0;
                            }
                            epgProgramView.scrollViewer.ScrollToVerticalOffset(Math.Ceiling(pos));
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var ps = PresentationSource.FromVisual(this);
            if (ps != null)
            {
                //高DPI環境でProgramViewの位置を物理ピクセルに合わせるためにヘッダの幅を微調整する
                //RootにUseLayoutRoundingを適用できれば不要だがボタン等が低品質になるので自力でやる
                Point p = grid_PG.TransformToVisual(ps.RootVisual).Transform(new Point(40, 80));
                Matrix m = ps.CompositionTarget.TransformToDevice;
                grid_PG.ColumnDefinitions[0].Width = new GridLength(40 + Math.Floor(p.X * m.M11) / m.M11 - p.X);
                grid_PG.RowDefinitions[1].Height = new GridLength(40 + Math.Floor(p.Y * m.M22) / m.M22 - p.Y);
            }
        }

        private bool ReloadEpgData()
        {
            try
            {
                //EpgViewPanelがDPI倍率の情報を必要とするため
                if (PresentationSource.FromVisual(Application.Current.MainWindow) != null)
                {
                    if (setViewInfo.SearchMode == true)
                    {
                        //番組情報の検索
                        var list = new List<EpgEventInfo>();
                        CommonManager.CreateSrvCtrl().SendSearchPg(new List<EpgSearchKeyInfo>() { setViewInfo.SearchKey }, ref list);
                        //サービス毎のリストに変換
                        var serviceEventList = new Dictionary<ulong, EpgServiceAllEventInfo>();
                        foreach (EpgEventInfo info in list)
                        {
                            ulong id = CommonManager.Create64Key(info.original_network_id, info.transport_stream_id, info.service_id);
                            if (serviceEventList.ContainsKey(id) == false)
                            {
                                if (ChSet5.Instance.ChList.ContainsKey(id) == false)
                                {
                                    //サービス情報ないので無効
                                    continue;
                                }
                                serviceEventList.Add(id, new EpgServiceAllEventInfo(CommonManager.ConvertChSet5To(ChSet5.Instance.ChList[id])));
                            }
                            serviceEventList[id].eventList.Add(info);
                        }
                        ReloadProgramViewItem(serviceEventList);
                    }
                    else
                    {
                        if (CommonManager.Instance.NWMode && CommonManager.Instance.NWConnectedIP == null)
                        {
                            return false;
                        }
                        ErrCode err = CommonManager.Instance.DB.ReloadEpgData();
                        if (err != ErrCode.CMD_SUCCESS)
                        {
                            if (IsVisible && err != ErrCode.CMD_ERR_BUSY)
                            {
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "EPGデータの取得でエラーが発生しました。EPGデータが読み込まれていない可能性があります。");
                                }), null);
                            }
                            return false;
                        }

                        ReloadProgramViewItem(CommonManager.Instance.DB.ServiceEventList);
                    }
                    MoveNowTime();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                }), null);
            }
            return false;
        }

        /// <summary>
        /// EPGデータ更新通知
        /// </summary>
        public void UpdateEpgData()
        {
            ClearInfo();
            updateEpgData = true;
            if (IsVisible || (Settings.Instance.NgAutoEpgLoadNW == false && Settings.Instance.PrebuildEpg))
            {
                if (ReloadEpgData() == true)
                {
                    updateEpgData = false;
                    ReloadReserveViewItem();
                    updateReserveData = false;
                }
            }
        }

        /// <summary>
        /// 予約情報更新通知
        /// </summary>
        public void RefreshReserve()
        {
            updateReserveData = true;
            if (this.IsVisible == true)
            {
                ReloadReserveViewItem();
                updateReserveData = false;
            }
        }

        /// <summary>
        /// 予約情報の再描画
        /// </summary>
        private void ReloadReserveViewItem()
        {
            reserveList.Clear();
            try
            {
                foreach (ReserveData info in CommonManager.Instance.DB.ReserveList.Values)
                {
                    {
                        int mergePos = 0;
                        int mergeNum = 0;
                        int servicePos = -1;
                        for (int i = 0; i < serviceList.Count; i++)
                        {
                            //TSIDが同じでSIDが逆順に登録されているときは併合する
                            if (--mergePos < i - mergeNum)
                            {
                                EpgServiceInfo curr = serviceList[i];
                                for (mergePos = i; mergePos + 1 < serviceList.Count; mergePos++)
                                {
                                    EpgServiceInfo next = serviceList[mergePos + 1];
                                    if (next.ONID != curr.ONID || next.TSID != curr.TSID || next.SID >= curr.SID)
                                    {
                                        break;
                                    }
                                    curr = next;
                                }
                                mergeNum = mergePos + 1 - i;
                                servicePos++;
                            }
                            EpgServiceInfo srvInfo = serviceList[mergePos];
                            if (srvInfo.ONID == info.OriginalNetworkID &&
                                srvInfo.TSID == info.TransportStreamID &&
                                srvInfo.SID == info.ServiceID)
                            {
                                ReserveViewItem viewItem = new ReserveViewItem(info);
                                viewItem.LeftPos = Settings.Instance.ServiceWidth * (servicePos + (double)((mergeNum + i - mergePos - 1) / 2) / mergeNum);

                                Int32 duration = (Int32)info.DurationSecond;
                                DateTime startTime = info.StartTime;
                                //総時間60秒を下限に縮小方向のマージンを反映させる
                                int startMargin = info.RecSetting.StartMargine;
                                int endMargin = info.RecSetting.EndMargine;
                                if (info.RecSetting.UseMargineFlag == 0)
                                {
                                    startMargin = 0;
                                    endMargin = 0;
                                    if (CommonManager.Instance.DB.DefaultRecSetting != null)
                                    {
                                        startMargin = CommonManager.Instance.DB.DefaultRecSetting.StartMargine;
                                        endMargin = CommonManager.Instance.DB.DefaultRecSetting.EndMargine;
                                    }
                                }
                                startMargin = Math.Max(startMargin, -(duration - 60));
                                endMargin = Math.Max(endMargin, -Math.Min(startMargin, 0) - (duration - 60));
                                startTime = startTime.AddSeconds(-Math.Min(startMargin, 0));
                                duration += Math.Min(startMargin, 0) + Math.Min(endMargin, 0);

                                viewItem.Height = Math.Floor((duration / 60) * Settings.Instance.MinHeight);
                                if (viewItem.Height < Settings.Instance.MinHeight)
                                {
                                    viewItem.Height = Settings.Instance.MinHeight;
                                }
                                viewItem.Width = Settings.Instance.ServiceWidth / mergeNum;

                                reserveList.Add(viewItem);
                                DateTime chkTime = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0);
                                if (timeList.ContainsKey(chkTime) == true)
                                {
                                    bool modified = false;
                                    if (Settings.Instance.MinimumHeight > 0 && viewItem.ReserveInfo.EventID != 0xFFFF)
                                    {
                                        //予約情報から番組情報を特定し、枠表示位置を再設定する
                                        foreach (ProgramViewItem pgInfo in timeList[chkTime])
                                        {
                                            if (pgInfo.Past == false &&
                                                viewItem.ReserveInfo.OriginalNetworkID == pgInfo.EventInfo.original_network_id &&
                                                viewItem.ReserveInfo.TransportStreamID == pgInfo.EventInfo.transport_stream_id &&
                                                viewItem.ReserveInfo.ServiceID == pgInfo.EventInfo.service_id &&
                                                viewItem.ReserveInfo.EventID == pgInfo.EventInfo.event_id &&
                                                info.DurationSecond != 0)
                                            {
                                                viewItem.TopPos = pgInfo.TopPos + pgInfo.Height * (startTime - info.StartTime).TotalSeconds / info.DurationSecond;
                                                viewItem.Width = pgInfo.Width;
                                                viewItem.Height = Math.Max(pgInfo.Height * duration / info.DurationSecond, Settings.Instance.MinHeight);
                                                modified = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (modified == false)
                                    {
                                        int index = timeList.IndexOfKey(chkTime);
                                        viewItem.TopPos = index * 60 * Settings.Instance.MinHeight;
                                        viewItem.TopPos += Math.Floor((startTime - chkTime).TotalMinutes * Settings.Instance.MinHeight);
                                        foreach (ProgramViewItem pgInfo in timeList.Values[index])
                                        {
                                            if (pgInfo.LeftPos == viewItem.LeftPos && pgInfo.TopPos <= viewItem.TopPos && viewItem.TopPos < pgInfo.TopPos + pgInfo.Height)
                                            {
                                                viewItem.Width = pgInfo.Width;
                                                break;
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
                epgProgramView.SetReserveList(reserveList);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 番組情報の再描画処理
        /// </summary>
        private void ReloadProgramViewItem(Dictionary<ulong, EpgServiceAllEventInfo> serviceEventList)
        {
            try
            {
                epgProgramView.ClearInfo();
                timeList.Clear();
                var programList = new List<ProgramViewItem>();
                nowViewTimer.Stop();

                //必要サービスの抽出
                serviceList.Clear();

                foreach (ulong id in setViewInfo.ViewServiceList)
                {
                    if (serviceEventList.ContainsKey(id) == true)
                    {
                        EpgServiceInfo serviceInfo = serviceEventList[id].serviceInfo;
                        if (serviceList.Exists(i => i.ONID == serviceInfo.ONID && i.TSID == serviceInfo.TSID && i.SID == serviceInfo.SID) == false)
                        {
                            serviceList.Add(serviceInfo);
                        }
                    }
                }
                List<ushort> contentKindList = setViewInfo.ViewContentKindList.ToList();
                contentKindList.Sort();


                //必要番組の抽出と時間チェック
                List<EpgServiceInfo> primeServiceList = new List<EpgServiceInfo>();
                //番組表でまとめて描画する矩形の幅と番組集合のリスト
                var programGroupList = new List<Tuple<double, List<ProgramViewItem>>>();
                int groupSpan = 1;
                int mergePos = 0;
                int mergeNum = 0;
                int servicePos = -1;
                for (int i = 0; i < serviceList.Count; i++)
                {
                    //TSIDが同じでSIDが逆順に登録されているときは併合する
                    int spanCheckNum = 1;
                    if (--mergePos < i - mergeNum)
                    {
                        EpgServiceInfo curr = serviceList[i];
                        for (mergePos = i; mergePos + 1 < serviceList.Count; mergePos++)
                        {
                            EpgServiceInfo next = serviceList[mergePos + 1];
                            if (next.ONID != curr.ONID || next.TSID != curr.TSID || next.SID >= curr.SID)
                            {
                                break;
                            }
                            curr = next;
                        }
                        mergeNum = mergePos + 1 - i;
                        servicePos++;
                        //正順のときは貫きチェックするサービス数を調べる
                        for (; mergeNum == 1 && i + spanCheckNum < serviceList.Count; spanCheckNum++)
                        {
                            EpgServiceInfo next = serviceList[i + spanCheckNum];
                            if (next.ONID != curr.ONID || next.TSID != curr.TSID)
                            {
                                break;
                            }
                            else if (next.SID < curr.SID)
                            {
                                spanCheckNum--;
                                break;
                            }
                            curr = next;
                        }
                        if (--groupSpan <= 0)
                        {
                            groupSpan = spanCheckNum;
                            programGroupList.Add(new Tuple<double, List<ProgramViewItem>>(Settings.Instance.ServiceWidth * groupSpan, new List<ProgramViewItem>()));
                        }
                        primeServiceList.Add(serviceList[mergePos]);
                    }

                    EpgServiceInfo serviceInfo = serviceList[mergePos];
                    UInt64 id = CommonManager.Create64Key(serviceInfo.ONID, serviceInfo.TSID, serviceInfo.SID);
                    int eventInfoIndex = -1;
                    foreach (EpgEventInfo eventInfo in Enumerable.Concat(serviceEventList[id].eventArcList, serviceEventList[id].eventList))
                    {
                        bool past = ++eventInfoIndex < serviceEventList[id].eventArcList.Count;
                        if (eventInfo.StartTimeFlag == 0)
                        {
                            //開始未定は除外
                            continue;
                        }
                        //ジャンル絞り込み
                        if (contentKindList.Count > 0)
                        {
                            bool find = false;
                            if (eventInfo.ContentInfo == null || eventInfo.ContentInfo.nibbleList.Count == 0)
                            {
                                //ジャンル情報ない
                                find = contentKindList.BinarySearch(0xFFFF) >= 0;
                            }
                            else
                            {
                                foreach (EpgContentData contentInfo in eventInfo.ContentInfo.nibbleList)
                                {
                                    int nibble1 = contentInfo.content_nibble_level_1;
                                    int nibble2 = contentInfo.content_nibble_level_2;
                                    if (nibble1 == 0x0E && nibble2 <= 0x01)
                                    {
                                        nibble1 = contentInfo.user_nibble_1 | (0x60 + nibble2 * 16);
                                        nibble2 = contentInfo.user_nibble_2;
                                    }
                                    if (contentKindList.BinarySearch((ushort)(nibble1 << 8 | 0xFF)) >= 0 ||
                                        contentKindList.BinarySearch((ushort)(nibble1 << 8 | nibble2)) >= 0)
                                    {
                                        find = true;
                                        break;
                                    }
                                }
                            }
                            if (find == false)
                            {
                                //ジャンル見つからないので除外
                                continue;
                            }
                        }
                        //イベントグループのチェック
                        int widthSpan = 1;
                        if (eventInfo.EventGroupInfo != null)
                        {
                            bool spanFlag = false;
                            foreach (EpgEventData data in eventInfo.EventGroupInfo.eventDataList)
                            {
                                if (serviceInfo.ONID == data.original_network_id &&
                                    serviceInfo.TSID == data.transport_stream_id &&
                                    serviceInfo.SID == data.service_id)
                                {
                                    spanFlag = true;
                                    break;
                                }
                            }

                            if (spanFlag == false)
                            {
                                //サービス２やサービス３の結合されるべきもの
                                continue;
                            }
                            else
                            {
                                //横にどれだけ貫くかチェック
                                int count = 1;
                                while (mergeNum == 1 ? count < spanCheckNum : count < mergeNum - (mergeNum+i-mergePos-1)/2)
                                {
                                    EpgServiceInfo nextInfo = serviceList[mergeNum == 1 ? i + count : mergePos - count];
                                    bool findNext = false;
                                    foreach (EpgEventData data in eventInfo.EventGroupInfo.eventDataList)
                                    {
                                        if (nextInfo.ONID == data.original_network_id &&
                                            nextInfo.TSID == data.transport_stream_id &&
                                            nextInfo.SID == data.service_id)
                                        {
                                            widthSpan++;
                                            findNext = true;
                                        }
                                    }
                                    if (findNext == false)
                                    {
                                        break;
                                    }
                                    count++;
                                }
                            }
                        }

                        ProgramViewItem viewItem = new ProgramViewItem(eventInfo, past);
                        viewItem.Height = ((eventInfo.DurationFlag == 0 ? 300 : eventInfo.durationSec) * Settings.Instance.MinHeight) / 60;
                        viewItem.Width = Settings.Instance.ServiceWidth * widthSpan / mergeNum;
                        viewItem.LeftPos = Settings.Instance.ServiceWidth * (servicePos + (double)((mergeNum+i-mergePos-1)/2) / mergeNum);
                        //viewItem.TopPos = (eventInfo.start_time - startTime).TotalMinutes * Settings.Instance.MinHeight;
                        programGroupList[programGroupList.Count - 1].Item2.Add(viewItem);
                        programList.Add(viewItem);

                        //日付チェック
                        DateTime EndTime;
                        if (eventInfo.DurationFlag == 0)
                        {
                            //終了未定
                            EndTime = eventInfo.start_time.AddSeconds(30 * 10);
                        }
                        else
                        {
                            EndTime = eventInfo.start_time.AddSeconds(eventInfo.durationSec);
                        }
                        //必要時間リストの構築
                        DateTime chkStartTime = new DateTime(eventInfo.start_time.Year, eventInfo.start_time.Month, eventInfo.start_time.Day, eventInfo.start_time.Hour, 0, 0);
                        while (chkStartTime <= EndTime)
                        {
                            if (timeList.ContainsKey(chkStartTime) == false)
                            {
                                timeList.Add(chkStartTime, new List<ProgramViewItem>());
                            }
                            chkStartTime = chkStartTime.AddHours(1);
                        }
                    }
                }

                //必要時間のチェック
                if (setViewInfo.NeedTimeOnlyBasic == false)
                {
                    //番組のない時間帯を追加
                    for (int i = 1; i < timeList.Count; i++)
                    {
                        if (timeList.Keys[i] > timeList.Keys[i - 1].AddHours(1))
                        {
                            timeList.Add(timeList.Keys[i - 1].AddHours(1), new List<ProgramViewItem>());
                        }
                    }

                    //番組の表示位置設定
                    foreach (ProgramViewItem item in programList)
                    {
                        item.TopPos = (item.EventInfo.start_time - timeList.Keys[0]).TotalMinutes * Settings.Instance.MinHeight;
                    }
                }
                else
                {
                    //番組の表示位置設定
                    foreach (ProgramViewItem item in programList)
                    {
                        DateTime chkStartTime = new DateTime(item.EventInfo.start_time.Year,
                            item.EventInfo.start_time.Month,
                            item.EventInfo.start_time.Day,
                            item.EventInfo.start_time.Hour,
                            0,
                            0);
                        if (timeList.ContainsKey(chkStartTime) == true)
                        {
                            int index = timeList.IndexOfKey(chkStartTime);
                            item.TopPos = (index * 60 + (item.EventInfo.start_time - chkStartTime).TotalMinutes) * Settings.Instance.MinHeight;
                        }
                    }
                }

                if (Settings.Instance.MinimumHeight > 0)
                {
                    //最低表示行数を適用
                    programList.Sort((x, y) => Math.Sign(x.LeftPos - y.LeftPos) * 2 + Math.Sign(x.TopPos - y.TopPos));
                    double minimum = Math.Floor((Settings.Instance.FontSizeTitle + 2) * Settings.Instance.MinimumHeight);
                    double lastLeft = double.MinValue;
                    double lastBottom = 0;
                    foreach (ProgramViewItem item in programList)
                    {
                        if (lastLeft != item.LeftPos)
                        {
                            lastLeft = item.LeftPos;
                            lastBottom = double.MinValue;
                        }
                        item.Height = Math.Max(item.Height, minimum);
                        if (item.TopPos < lastBottom)
                        {
                            item.Height = Math.Max(item.TopPos + item.Height - lastBottom, minimum);
                            item.TopPos = lastBottom;
                        }
                        lastBottom = item.TopPos + item.Height;
                    }
                }

                //必要時間リストと時間と番組の関連づけ
                foreach (ProgramViewItem item in programList)
                {
                    int index = Math.Max((int)(item.TopPos / (60 * Settings.Instance.MinHeight)), 0);
                    while (index < Math.Min((int)((item.TopPos + item.Height) / (60 * Settings.Instance.MinHeight)) + 1, timeList.Count))
                    {
                        timeList.Values[index++].Add(item);
                    }
                }

                epgProgramView.SetProgramList(
                    programGroupList,
                    timeList.Count * 60 * Settings.Instance.MinHeight);

                List<DateTime> dateTimeList = new List<DateTime>();
                foreach (var item in timeList)
                {
                    dateTimeList.Add(item.Key);
                }
                timeView.SetTime(dateTimeList, setViewInfo.NeedTimeOnlyBasic, false);
                dateView.SetTime(dateTimeList);
                serviceView.SetService(primeServiceList);

                ReDrawNowLine();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible == false) { return; }

            if (updateEpgData && ReloadEpgData())
            {
                updateEpgData = false;
                ReloadReserveViewItem();
                updateReserveData = false;
            }
            if (updateReserveData)
            {
                ReloadReserveViewItem();
                updateReserveData = false;
            }
            if (scrollToTarget != null)
            {
                ScrollTo(scrollToTarget);
            }
        }

        public void ScrollTo(object target)
        {
            scrollToTarget = null;
            if (IsVisible == false)
            {
                //Visibleになるまですこし待つ
                scrollToTarget = target;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => scrollToTarget = null));
                return;
            }
            if (target is ReserveData)
            {
                foreach (ReserveViewItem reserveViewItem1 in this.reserveList)
                {
                    if (reserveViewItem1.ReserveInfo.ReserveID == ((ReserveData)target).ReserveID)
                    {
                        this.epgProgramView.scrollViewer.ScrollToHorizontalOffset(reserveViewItem1.LeftPos - 100);
                        this.epgProgramView.scrollViewer.ScrollToVerticalOffset(reserveViewItem1.TopPos - 100);
                        break;
                    }
                }
            }
            else if (target is EpgEventInfo)
            {
                for (int i = 0; i < this.timeList.Count; i++)
                {
                    foreach (ProgramViewItem item in this.timeList.Values[i])
                    {
                        if (item.Past == false &&
                            item.EventInfo.event_id == ((EpgEventInfo)target).event_id &&
                            item.EventInfo.original_network_id == ((EpgEventInfo)target).original_network_id &&
                            item.EventInfo.service_id == ((EpgEventInfo)target).service_id &&
                            item.EventInfo.transport_stream_id == ((EpgEventInfo)target).transport_stream_id)
                        {
                            this.epgProgramView.scrollViewer.ScrollToHorizontalOffset(item.LeftPos - 100);
                            this.epgProgramView.scrollViewer.ScrollToVerticalOffset(item.TopPos - 100);
                            i = this.timeList.Count - 1;
                            break;
                        }
                    }
                }
            }
        }
    }
}
