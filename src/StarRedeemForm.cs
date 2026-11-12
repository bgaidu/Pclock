using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCLock
{
    /// <summary>
    /// P1-11 星星兑换窗口：孩子自助把星星换成使用时间（不需要 PIN）。
    /// 兑换受「余额」和「每日上限」双重限制；每次兑换写审计日志。
    /// </summary>
    public class StarRedeemForm : Form
    {
        string user;
        int perStar;
        int balance;
        int usedSeconds;
        int dailyLimitMinutes;
        int maxStars;

        Label balanceLabel;
        Label dailyLabel;
        NumericUpDown starBox;
        Label previewLabel;
        Button redeemBtn;

        public StarRedeemForm()
        {
            user = Store.GetCurrentUser();
            perStar = Store.GetStarSecondsPerStar(user);
            balance = Store.GetStarBalance(user);
            usedSeconds = Store.GetStarRedeemedSecondsToday(user);
            dailyLimitMinutes = Store.GetStarDailyLimitMinutes(user);
            maxStars = StarWallet.MaxRedeemableStars(balance, perStar, usedSeconds, dailyLimitMinutes);

            Text = "星星兑换";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 336);
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Microsoft YaHei", 9);

            Label title = new Label();
            title.Text = "星星兑换";
            title.Font = new Font("Microsoft YaHei", 16, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            balanceLabel = new Label();
            balanceLabel.Text = Strings.Fmt("当前星星：{0} 星", balance);
            balanceLabel.Font = new Font("Microsoft YaHei", 13);
            balanceLabel.AutoSize = true;
            balanceLabel.Location = new Point(20, 62);
            Controls.Add(balanceLabel);

            Label rate = new Label();
            rate.Text = Strings.Fmt("兑换比例：1 星 = {0} 秒（家长可在高级设置修改）", perStar);
            rate.ForeColor = Color.DimGray;
            rate.AutoSize = true;
            rate.Location = new Point(20, 98);
            Controls.Add(rate);

            dailyLabel = new Label();
            dailyLabel.Text = DailyText();
            dailyLabel.ForeColor = Color.DimGray;
            dailyLabel.AutoSize = true;
            dailyLabel.Location = new Point(20, 124);
            Controls.Add(dailyLabel);

            Label pick = new Label();
            pick.Text = "兑换星数：";
            pick.AutoSize = true;
            pick.Location = new Point(20, 168);
            Controls.Add(pick);

            starBox = new NumericUpDown();
            starBox.Location = new Point(110, 165);
            starBox.Size = new Size(90, 26);
            starBox.Minimum = 1;
            starBox.Maximum = maxStars > 0 ? maxStars : 1;
            starBox.Value = 1;
            starBox.ValueChanged += delegate { RefreshPreview(); };
            if (maxStars <= 0) starBox.Enabled = false;
            Controls.Add(starBox);

            previewLabel = new Label();
            previewLabel.Font = new Font("Microsoft YaHei", 11, FontStyle.Bold);
            previewLabel.AutoSize = true;
            previewLabel.Location = new Point(20, 205);
            Controls.Add(previewLabel);

            redeemBtn = new Button();
            redeemBtn.Text = "兑换";
            redeemBtn.Size = new Size(100, 34);
            redeemBtn.Location = new Point(200, 250);
            redeemBtn.Enabled = maxStars > 0;
            redeemBtn.Click += delegate { Redeem(); };
            Controls.Add(redeemBtn);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Size = new Size(100, 34);
            cancel.Location = new Point(310, 250);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            Label hint = new Label();
            hint.Text = "兑换后立即增加本次使用时间；每天 0 点重置额度。";
            hint.ForeColor = Color.DimGray;
            hint.AutoSize = true;
            hint.Location = new Point(20, 296);
            Controls.Add(hint);

            RefreshPreview();
        }

        string DailyText()
        {
            int m = usedSeconds / 60;
            int s = usedSeconds % 60;
            return dailyLimitMinutes > 0
                ? Strings.Fmt("今日已兑换：{0} 分 {1} 秒（上限 {2} 分钟）", m, s, dailyLimitMinutes)
                : Strings.Fmt("今日已兑换：{0} 分 {1} 秒（不限）", m, s);
        }

        void RefreshPreview()
        {
            if (maxStars <= 0)
            {
                previewLabel.Text = balance <= 0
                    ? Strings.Get("星星不足，先去答题赚星星吧。")
                    : Strings.Get("今日额度已用完，明天再来。");
                return;
            }
            int stars = (int)starBox.Value;
            int seconds = StarWallet.SecondsFor(stars, perStar);
            previewLabel.Text = Strings.Fmt("可兑换：{0} 分 {1} 秒", seconds / 60, seconds % 60);
        }

        void Redeem()
        {
            int stars = (int)starBox.Value;
            if (stars < 1 || stars > maxStars) return;
            int seconds = StarWallet.SecondsFor(stars, perStar);
            if (seconds <= 0) return;

            try
            {
                Store.AddStarBalance(user, -stars);
                Store.AddStarRedeemedSeconds(user, seconds);
                if (App.Instance != null) App.Instance.AddTimeSeconds(seconds);
                AuditLog.Write("星星兑换：用户=" + user + "，扣 " + stars + " 星，增加 " +
                    (seconds / 60) + " 分 " + (seconds % 60) + " 秒");
                MessageBox.Show(Strings.Fmt("已兑换 {0} 分 {1} 秒使用时间。", seconds / 60, seconds % 60),
                    Strings.Get("星星兑换"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Fmt("兑换失败：{0}", ex.Message), Strings.Get("星星兑换"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
