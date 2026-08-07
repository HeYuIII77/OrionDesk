using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using UserControl = System.Windows.Controls.UserControl;

namespace OrionDesk.UI.Controls
{
    /// <summary>
    /// 暗色下拉框控件 - 替代标准 ComboBox，完全暗色主题
    /// </summary>
    public partial class DarkComboBox : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 下拉选项集合
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<DarkComboBoxItem>),
                typeof(DarkComboBox), new PropertyMetadata(null, OnItemsSourceChanged));

        /// <summary>
        /// 当前选中项的 Tag 值
        /// </summary>
        public static readonly DependencyProperty SelectedTagProperty =
            DependencyProperty.Register(nameof(SelectedTag), typeof(string),
                typeof(DarkComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTagChanged));

        /// <summary>
        /// 当前选中项
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(DarkComboBoxItem),
                typeof(DarkComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ObservableCollection<DarkComboBoxItem> ItemsSource
        {
            get => (ObservableCollection<DarkComboBoxItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string? SelectedTag
        {
            get => (string?)GetValue(SelectedTagProperty);
            set => SetValue(SelectedTagProperty, value);
        }

        public DarkComboBoxItem? SelectedItem
        {
            get => (DarkComboBoxItem?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        /// <summary>
        /// 选项变更事件
        /// </summary>
        public event EventHandler<DarkComboBoxItem?>? SelectionChanged;

        #endregion

        public DarkComboBox()
        {
            InitializeComponent();

            // 点击外部关闭下拉
            AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, new RoutedEventHandler(OnMouseDownOutside));

            // 加载完成后设置初始显示
            Loaded += (s, e) => UpdateDisplay();
        }

        #region 属性变更回调

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DarkComboBox ctrl)
            {
                ctrl.ItemList.ItemsSource = e.NewValue as ObservableCollection<DarkComboBoxItem>;
                ctrl.UpdateDisplay();
            }
        }

        private static void OnSelectedTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DarkComboBox ctrl)
                ctrl.SyncFromTag();
        }

        #endregion

        #region 事件处理

        private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemList.SelectedItem is DarkComboBoxItem item)
            {
                SelectedItem = item;
                SelectedTag = item.Tag;
                UpdateDisplay();
                Popup.IsOpen = false;
                SelectionChanged?.Invoke(this, item);
            }
        }

        private void OnMouseDownOutside(object? sender, RoutedEventArgs e)
        {
            Popup.IsOpen = false;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据 SelectedTag 同步选中项
        /// </summary>
        private void SyncFromTag()
        {
            if (ItemsSource == null) return;
            foreach (var item in ItemsSource)
            {
                if (item.Tag == SelectedTag)
                {
                    SelectedItem = item;
                    ItemList.SelectedItem = item;
                    UpdateDisplay();
                    return;
                }
            }
        }

        /// <summary>
        /// 更新显示文本
        /// </summary>
        private void UpdateDisplay()
        {
            if (SelectedItem != null)
                DisplayText.Text = SelectedItem.DisplayText;
            else if (ItemsSource?.Count > 0)
                DisplayText.Text = ItemsSource[0].DisplayText;
            else
                DisplayText.Text = "";
        }

        #endregion
    }

    /// <summary>
    /// 下拉框选项
    /// </summary>
    public class DarkComboBoxItem
    {
        /// <summary>显示文本</summary>
        public string DisplayText { get; set; } = "";

        /// <summary>标识值</summary>
        public string Tag { get; set; } = "";

        public override string ToString() => DisplayText;
    }
}
