﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ImageTools_GDI.control;

namespace ImageTools_GDI
{
    public partial class FrmTool : Form
    {
        [DllImport("PrScrn.dll", EntryPoint = "PrScrn")]
        public extern static int PrScrn();


        public CutPicPanelTools ScrnTools = null;
        CutPicPanel panel1 = null;

        /// <summary>
        /// 裁剪图片
        /// </summary>
        Boolean isCut = false;

        /// <summary>
        /// 移动图片
        /// </summary>
        Boolean isPicMove = true;

        /// <summary>
        /// 移动矩形
        /// </summary>
        Boolean isRectMove = false;


        /// <summary>
        /// 当前鼠标按下的坐标
        /// </summary>
        Point m_now_point;

        /// <summary>
        /// 鼠标是否按下
        /// </summary>
        Boolean m_down = false;

        /// <summary>
        /// 原始图片
        /// </summary>
        Image orig_image = null;

        /// <summary>
        /// 操作后的图片
        /// </summary>
        Image src_image = null;

        #region 矩形选矿
        /// <summary>
        /// 矩形选框的工具栏
        /// </summary>
        Boolean show_tools = false;

        /// <summary>
        /// 当前鼠标坐标(拖动矩形选框用)
        /// </summary>
        Point rect_basepoint;

        /// <summary>
        /// 矩形选框移动次数
        /// </summary>
        int rect_move_count;

        Bitmap bitmap = null;
        Graphics graph;
        Pen pen;
        Rectangle draw_rect;
        #endregion

        #region 缩放拖拽

        Point mouseDownPoint;
        int width, height;
        /// <summary>
        /// 宽高比
        /// </summary>
        decimal percent = 0m;
        /// <summary>
        /// 缩放
        /// </summary>
        Rectangle rect;
        #endregion

        private void btnScrn_Click(Object sender, EventArgs e)
        {
            if (PrScrn() == 1)
            {
                if (Clipboard.ContainsImage())
                {
                    panelImage.BackgroundImage = Clipboard.GetImage();
                }
            }
        }
        public FrmTool()
        {
            InitializeComponent();

            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            panel1 = new CutPicPanel();
            panel1.BackColor = Color.Transparent;
            panel1.BorderStyle = BorderStyle.FixedSingle;
            panel1.MouseDown += this.FrmScrn_MouseDown;
            panel1.MouseMove += this.FrmScrn_MouseMove;
            panel1.MouseUp += this.FrmScrn_MouseUp;
            panel1.MouseWheel += this.Panel1_MouseWheel;
            panel1.DragDrop += Panel1_DragDrop;
            panel1.DragEnter += Panel1_DragEnter;
            panel1.Dock = DockStyle.Fill;
            panel1.BringToFront();
            panel1.AllowDrop = true;
            picImage.Controls.Add(panel1);
            tabControl1.SelectedIndex = 1;
        }

        private void Panel1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Panel1_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();       //获得路径
            orig_image = Image.FromFile(path);
            src_image = Image.FromFile(path);

            width = src_image.Width;
            height = src_image.Height;
            percent = Convert.ToDecimal(width) / Convert.ToDecimal(height);
            rect = new Rectangle(0, 0, width, height);
            picImage.Invalidate();
        }

        private void Panel1_MouseWheel(Object sender, MouseEventArgs e)
        {
            width += (e.Delta / 5);
            height = Convert.ToInt32(width / percent);
            picImage.Invalidate();
        }

        private void FrmScrn_MouseUp(Object sender, MouseEventArgs e)
        {
            m_down = false;
            isRectMove = false;
            isPicMove = false;
            if (show_tools && draw_rect.Height > 0 && draw_rect.Width > 0)
            {
                ScrnTools = new CutPicPanelTools();
                ScrnTools.Size = new System.Drawing.Size(72, 30);
                ScrnTools.Location = new System.Drawing.Point(draw_rect.X + draw_rect.Width - ScrnTools.Width, draw_rect.Y + draw_rect.Height + 2);
                ScrnTools.BringToFront();
                ScrnTools.Visible = true;
                ScrnTools.ToolsClickEvent += this.ScrnTools_ToolsClickEvent;
                ScrnTools.Show();
                panel1.Controls.Add(ScrnTools);
            }
            if (isPicMove)
            {
                isPicMove = false;
            }
            Cursor.Current = Cursors.Default;
        }

        private void ScrnTools_ToolsClickEvent(Object sender, EventArgs e)
        {
            if ((int)sender == 1)
            {
                Point r = new Point();
                Bitmap image = new Bitmap(draw_rect.Width, draw_rect.Height);
                Graphics imgGh = Graphics.FromImage(image);
                r.X = draw_rect.X ;
                r.Y = draw_rect.Y ;
                r = panel1.PointToScreen(r);
                imgGh.CopyFromScreen(r, new Point(0, 0), new Size(draw_rect.Width , draw_rect.Height ));

                picScrn.Image = image;
            }
            bitmap = new Bitmap(panel1.Width, panel1.Height);
            graph = Graphics.FromImage(bitmap);
            graph.Clear(Color.Transparent);
            panel1.BackgroundImage = bitmap;
            draw_rect = new Rectangle(0, 0, 0, 0);
            ScrnTools.Visible = false;
            ScrnTools.Dispose();
            panel1.Controls.Clear();
            show_tools = false;
        }

        private void FrmScrn_MouseMove(Object sender, MouseEventArgs e)
        {
            if (m_down)//左键按下时
            {
                //gdi绘制区域截图砍掉，使用微信截图dll
                if (isCut)
                {
                    if (isRectMove)
                    {
                        //移动矩形框
                        draw_rect.X = draw_rect.X + (e.X - m_now_point.X);
                        draw_rect.Y = draw_rect.Y + (e.Y - m_now_point.Y);

                        //判断是否超过左上角           
                        if (draw_rect.X < 0)
                            draw_rect.X = 0;
                        if (draw_rect.Y < 0)
                            draw_rect.Y = 0;
                        //判断是否超过右下 角     
                        if (draw_rect.X > (this.Width - draw_rect.Width - 1))
                            draw_rect.X = this.Width - draw_rect.Width - 1;
                        if (draw_rect.Y > (this.Height - draw_rect.Height - 1))
                            draw_rect.Y = this.Height - draw_rect.Height - 1;
                        //画图              
                        bitmap = new Bitmap(panel1.Width, panel1.Height);
                        graph = Graphics.FromImage(bitmap);
                        pen = new Pen(Color.Red, 1.0f);
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

                        //graph.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.LightGreen)), draw_rect);
                        graph.DrawRectangle(pen, draw_rect);

                        m_now_point.X = e.X;
                        m_now_point.Y = e.Y;
                        rect_move_count++;
                        show_tools = true;
                        panel1.BackgroundImage = bitmap;
                        graph.Dispose();
                        pen.Dispose();
                    }
                    else
                    {
                        //画矩形
                        bitmap = new Bitmap(panel1.Width, panel1.Height);
                        graph = Graphics.FromImage(bitmap);
                        pen = new Pen(Color.Red, 1.0f);
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                        if (e.X < rect_basepoint.X && e.Y < rect_basepoint.Y)
                        {
                            draw_rect = new Rectangle(e.X, e.Y, System.Math.Abs(e.X - rect_basepoint.X), System.Math.Abs(e.Y - rect_basepoint.Y));
                            graph.DrawRectangle(pen, draw_rect);
                            //graph.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.LightGreen)), draw_rect);
                        }
                        else if (e.X > rect_basepoint.X && e.Y < rect_basepoint.Y)
                        {
                            draw_rect = new Rectangle(rect_basepoint.X, e.Y, System.Math.Abs(e.X - rect_basepoint.X), System.Math.Abs(e.Y - rect_basepoint.Y));
                            graph.DrawRectangle(pen, draw_rect);
                            //graph.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.LightGreen)), draw_rect);
                        }
                        else if (e.X < rect_basepoint.X && e.Y > rect_basepoint.Y)
                        {
                            draw_rect = new Rectangle(e.X, rect_basepoint.Y, System.Math.Abs(e.X - rect_basepoint.X), System.Math.Abs(e.Y - rect_basepoint.Y));
                            graph.DrawRectangle(pen, draw_rect);
                            //graph.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.LightGreen)), draw_rect);
                        }
                        else
                        {
                            draw_rect = new Rectangle(rect_basepoint.X, rect_basepoint.Y, System.Math.Abs(e.X - rect_basepoint.X), System.Math.Abs(e.Y - rect_basepoint.Y));
                            graph.DrawRectangle(pen, draw_rect);
                            //graph.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.LightGreen)), draw_rect);
                        }
                    }
                    //绘制完成显示工具栏
                    show_tools = true;
                    panel1.BackgroundImage = bitmap;
                    graph.Dispose();
                    pen.Dispose();
                }
                else if (isPicMove)
                {
                    if (rect.Contains(e.Location))
                    {
                        Cursor.Current = Cursors.Hand;
                        Point nowPoint = e.Location;
                        nowPoint.Offset(-mouseDownPoint.X, -mouseDownPoint.Y);
                        rect.Location = nowPoint;
                        picImage.Invalidate();
                    }
                    else
                        Cursor.Current = Cursors.Default;
                }
            }
        }

        private void FrmScrn_MouseDown(Object sender, MouseEventArgs e)
        {
            if (src_image == null) return;
            m_now_point = new Point(e.X, e.Y);
            if (e.Button == MouseButtons.Left)//画图，取消，拖动,旋转
            {
                if (draw_rect != null && draw_rect.Contains(m_now_point))
                {
                    //鼠标在矩形范围内拖动
                    isRectMove = true;
                    //isPicMove = false;
                }
                else
                {
                    rect_basepoint = e.Location;
                    isPicMove = true;

                    //鼠标不在矩形范围内，图片移动，清除矩形
                    bitmap = new Bitmap(panel1.Width, panel1.Height);
                    graph = Graphics.FromImage(bitmap);
                    graph.Clear(Color.Transparent);
                    panel1.BackgroundImage = bitmap;
                    draw_rect = new Rectangle(0, 0, 0, 0);

                    //图片移动
                    mouseDownPoint = e.Location;
                    mouseDownPoint.Offset(-rect.X, -rect.Y);
                    isPicMove = e.Button == MouseButtons.Left;
                    if (isPicMove && rect.Contains(e.Location))
                        Cursor.Current = Cursors.Hand;
                    else
                        Cursor.Current = Cursors.Default;
                }
                //清除矩形工具栏
                if (ScrnTools != null)
                {
                    ScrnTools.Visible = false;
                    ScrnTools.Dispose();
                    panel1.Controls.Clear();
                    ScrnTools = null;
                    show_tools = false;
                    isPicMove = false;
                }
            }
            m_down = true;
        }

        private void btnLeftRotate90_Click(Object sender, EventArgs e)
        {
            if (this.src_image == null) return;
            Image img = this.src_image;
            img.RotateFlip(RotateFlipType.Rotate270FlipNone);
            picImage.Invalidate();
        }

        private void btnRightRotate90_Click(Object sender, EventArgs e)
        {
            if (this.src_image == null) return;
            Image img = this.src_image;
            img.RotateFlip(RotateFlipType.Rotate90FlipNone);
            picImage.Invalidate();
        }

        private void btnVerFlip_Click(Object sender, EventArgs e)
        {
            if (this.src_image == null) return;
            Image img = this.src_image;
            img.RotateFlip(RotateFlipType.Rotate180FlipY);
            picImage.Invalidate();
        }

        private void btnHorFlip_Click(Object sender, EventArgs e)
        {
            if (this.src_image == null) return;
            Image img = this.src_image;
            img.RotateFlip(RotateFlipType.Rotate180FlipX);
            picImage.Invalidate();
        }

        private void btnOpenImage_Click(Object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                orig_image = Image.FromFile(ofd.FileName);
                src_image = Image.FromFile(ofd.FileName);

                width = src_image.Width;
                height = src_image.Height;
                percent = Convert.ToDecimal(width) / Convert.ToDecimal(height);

                var x = (picImage.Width / 2) - (src_image.Width / 2);
                var y = (picImage.Height / 2) - (src_image.Height / 2);
                rect = new Rectangle(x, y, width, height);
                picImage.Invalidate();
            }
        }

        private void picImage_Paint(Object sender, PaintEventArgs e)
        {
            base.OnPaint(e);
            if (src_image == null) return;
            if(width<=10 || height <= 10)
            {
                width = rect.Width;
                height = rect.Height;
            }

            rect.Width = width;
            rect.Height = height;
            e.Graphics.Clear(this.BackColor);
            RoatetImage(orig_image, e.Graphics, rect, tbRotate.Value);

            if (cbA4.Checked)
            {
                pen = new Pen(Color.Red, 2.0f);
                var rect = GetA4Rectangle();
                e.Graphics.DrawRectangle(pen, rect);
                e.Graphics.DrawString($"width:{rect.Width}", new Font("微软雅黑", 10), new SolidBrush(Color.Red), new Point(panel1.Width - 85, 0));
                e.Graphics.DrawString($"height:{rect.Height}", new Font("微软雅黑", 10), new SolidBrush(Color.Red), new Point(panel1.Width - 85, 15));
                //pen = new Pen(Color.Red, 3.0f);
                //for (int i = 0; i < pointList.Count; i++)
                //{
                //    e.Graphics.DrawRectangle(pen, new Rectangle(pointList[i],new Size(3,3)));
                //}
            }
            e.Graphics.DrawString($"width:{rect.Width}", new Font("微软雅黑", 10), new SolidBrush(Color.Red), new Point(0, 0));
            e.Graphics.DrawString($"height:{rect.Height}", new Font("微软雅黑", 10), new SolidBrush(Color.Red), new Point(0, 15));
        }

        private void btnCutPic_Click(Object sender, EventArgs e)
        {
            if (PrScrn() == 1)
            {
                if (Clipboard.ContainsImage())
                {
                    picScrn.Image = Clipboard.GetImage();
                }
            }
            //if (isCut)
            //{
            //    isCut = false;
            //    btnCutPic.BackColor = Color.Goldenrod;
            //}
            //else
            //{
            //    isCut = true;
            //    btnCutPic.BackColor = Color.DarkGoldenrod;
            //}
        }

        /// <summary>
        /// 获取A4大小矩形
        /// </summary>
        /// <returns></returns>
        public Rectangle GetA4Rectangle()
        {
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                float dpiX = graphics.DpiX;
                float dpiY = graphics.DpiY;

                int a4width = Convert.ToInt32((8.27 - 0.75 * 2) * dpiX);
                int a4height = Convert.ToInt32((11.69 - 0.75 * 2) * dpiY);

                int rectWidth = panel1.Width;
                int rectHeight = panel1.Height;


                //var newRectLocation = new Point();
                
                var res = AutoSize(rectWidth, rectHeight, a4width, a4height);

                var midPoint = panel1.Width / 2;
                var rectMidPoint = (res["Width"] - 4) / 2;
                var localX = midPoint - rectMidPoint;
                Rectangle a4;
                a4 = new Rectangle(localX, 1, res["Width"] - 4, res["Height"] - 4);
                //矩形四点坐标
                //var p1 = a4.Location;
                //var p2 = new Point(a4.X+a4.Width-3,a4.Y);
                //var p3 = new Point(a4.X,a4.Height-3);
                //var p4 = new Point(a4.X + a4.Width-3, a4.Height-3);
                return a4;
            }
        }

        public new Dictionary<string, int> AutoSize(int spcWidth, int spcHeight, int orgWidth, int orgHeight)
        {
            Dictionary<string, int> size = new Dictionary<string, int>();
            // 原始宽高在指定宽高范围内，不作任何处理 
            if (orgWidth <= spcWidth && orgHeight <= spcHeight)
            {
                size["Width"] = orgWidth;
                size["Height"] = orgHeight;
            }
            else
            {
                // 取得比例系数 
                float w = orgWidth / (float)spcWidth;
                float h = orgHeight / (float)spcHeight;
                // 宽度比大于高度比 
                if (w > h)
                {
                    size["Width"] = spcWidth;
                    size["Height"] = (int)(w >= 1 ? Math.Round(orgHeight / w) : Math.Round(orgHeight * w));
                }
                // 宽度比小于高度比 
                else if (w < h)
                {
                    size["Height"] = spcHeight;
                    size["Width"] = (int)(h >= 1 ? Math.Round(orgWidth / h) : Math.Round(orgWidth * h));
                }
                // 宽度比等于高度比 
                else
                {
                    size["Width"] = spcWidth;
                    size["Height"] = spcHeight;
                }
            }
            return size;
        }

        private void tbRotate_Scroll(Object sender, EventArgs e)
        {
            if (this.src_image == null) return;
            var angle = tbRotate.Value;
            label2.Text = $"{angle}°";
            picImage.Invalidate();
        }

        /// <summary>
        /// 以逆时针为方向对图像进行旋转
        /// </summary>
        /// <param name="b">位图流</param>
        /// <param name="angle">旋转角度[0,360](前台给的)</param>
        /// <returns></returns>
        public void RoatetImage(Image image, Graphics g, Rectangle r, int angle)
        {
            using (Matrix m = new Matrix())
            {
                m.RotateAt(angle, new PointF(r.Left + (r.Width / 2),
                                          r.Top + (r.Height / 2)));
                g.Transform = m;
                g.DrawImage(image, r);
                g.ResetTransform();
            }
        }

        private void FrmScrn_SizeChanged(Object sender, EventArgs e)
        {
            var x = (picImage.Width / 2) - (width / 2);
            var y = (picImage.Height / 2) - (height / 2);
            rect = new Rectangle(x, y, width, height);
            picImage.Invalidate();
        }

        private void cbA4_CheckedChanged(object sender, EventArgs e)
        {
            picImage.Invalidate();
        }

        private void btnCut_Click(object sender, EventArgs e)
        {
            Point r = new Point();
            var a4 = GetA4Rectangle();
            Bitmap image = new Bitmap(a4.Width-2, a4.Height-2);
            Graphics imgGh = Graphics.FromImage(image);
            r.X = a4.X+1;
            r.Y = a4.Y+1;
            r = panel1.PointToScreen(r);
            imgGh.CopyFromScreen(r, new Point(0, 0), new Size(a4.Width-2, a4.Height-2));

            picScrn.Image = image;
        }

        /// <summary>
        /// 旋转矩形
        /// </summary>
        /// <param name="g"></param>
        /// <param name="r"></param>
        /// <param name="angle"></param>
        public void RotateRectangle(Graphics g, Rectangle r, float angle)
        {
            using (Matrix m = new Matrix())
            {
                m.RotateAt(angle, new PointF(r.Left + (r.Width / 2),
                                          r.Top + (r.Height / 2)));
                g.Transform = m;
                g.DrawRectangle(new Pen(Color.Red, 1.0F), r);
                g.ResetTransform();
            }
        }
        /// <summary>
        /// 获取最小外界矩形
        /// </summary>
        /// <param name="width">原矩形的宽</param>
        /// <param name="height">原矩形高</param>
        /// <param name="angle">顺时针旋转角度</param>
        /// <returns></returns>
        public Rectangle GetRotateRectangle(int width, int height, float angle)
        {
            double radian = angle * Math.PI / 180 ;
            double cos = Math.Cos(radian);
            double sin = Math.Sin(radian);
            //只需要考虑到第四象限和第三象限的情况取大值(中间用绝对值就可以包括第一和第二象限)
            int newWidth = (int)(Math.Max(Math.Abs(width * cos - height * sin), Math.Abs(width * cos + height * sin)));
            int newHeight = (int)(Math.Max(Math.Abs(width * sin - height * cos), Math.Abs(width * sin + height * cos)));
            return new Rectangle(0, 0, newWidth, newHeight);
        }
    }
}
