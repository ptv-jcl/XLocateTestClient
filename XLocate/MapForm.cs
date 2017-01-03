using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.IO;
using System.Text;
using System.Windows.Forms;
using XServer;

namespace XLocate
{
    public partial class MapForm : Form
    {
        // 2011-09-16: Flickering pinpoints solved
        private System.Drawing.Point lastTooltip = new System.Drawing.Point(-1, -1);
        
        public const int BORDER = 10;
        public const int SCROLL = 40;
        public System.Drawing.SolidBrush BRUSH = new SolidBrush(System.Drawing.Color.FromArgb(128, 255,255,255));
        public System.Drawing.Font FONT = new System.Drawing.Font(new FontFamily("Arial"), 12,FontStyle.Regular);
        XMapWSService svcMap = new XMapWSService()
        {
            Credentials = new NetworkCredential(Properties.Settings.Default.xmap_username, Properties.Settings.Default.xmap_password)
        };
        MapSection mapSection = new MapSection() {
            center = new XServer.Point() {
                point = new PlainPoint() {
                    x = 0.0,
                    y = 0.0
                }
            },
            scale = 1000
        };
        Layer[] arrLayer;
        CallerContext ccMap = new CallerContext() { 
            log1 = Properties.Settings.Default.CallerContext_Log1,
            log2 = Properties.Settings.Default.CallerContext_Log2,
            log3 = Properties.Settings.Default.CallerContext_Log3
        };
        CallerContextProperty ccpCoordFormat = new CallerContextProperty();
        CallerContextProperty ccpResponseGeometry = new CallerContextProperty();
        CallerContextProperty ccpProfile = new CallerContextProperty();
        ImageInfo imageInfo = new ImageInfo() { 
            format = ImageFileFormat.BMP
        };
        MapParams mapParams = new MapParams() { 
            showScale = true,
            useMiles = false
        };
        Map map;
        
        public MapForm()
        {
            InitializeComponent();
        }

        public MapForm(string xmap_service, Layer[] argArrayLayer, string coordFormat, string mapprofile)
        {
            InitializeComponent();
            //
            arrLayer = argArrayLayer;
            // 
            List<CallerContextProperty> lstCCP = new List<CallerContextProperty>();
            ccpResponseGeometry.key = "ResponseGeometry";
            ccpResponseGeometry.value = "WKT";
            lstCCP.Add(ccpResponseGeometry);
            if (coordFormat != null)
            {
                ccpCoordFormat.key = "CoordFormat";
                ccpCoordFormat.value = coordFormat;
                lstCCP.Add(ccpCoordFormat);
            }
            if (mapprofile != "")
            {
                ccpProfile.key = "Profile";
                ccpProfile.value = mapprofile;
                lstCCP.Add(ccpProfile);
            }

            ccMap.wrappedProperties = lstCCP.ToArray();
            //
            svcMap.Url = xmap_service;
            renderMap();
            setLayerCentering(this.arrLayer, false);
            //
            this.Visible = false;
        }

        private void MapForm_Load(object sender, EventArgs e)
        {

        }

        public void setLayerCentering(Layer[] arrLayer, bool centerObjects)
        {
            foreach (Layer cLayer in arrLayer)
            {
                if (typeof(CustomLayer) == cLayer.GetType())
                {
                    ((CustomLayer)cLayer).centerObjects = centerObjects;
                }
            }
        }

        public void renderMap()
        {
            try
            { 
                int height = pbxMap.Height,width = pbxMap.Width;
                imageInfo.width = width;
                imageInfo.height = height;
                if ((width > 0) && (height > 0))
                {
                    map = svcMap.renderMap(mapSection, mapParams, imageInfo, arrLayer, true, ccMap);
                    mapSection.center = map.visibleSection.center;
                    mapSection.scale = map.visibleSection.scale;
                    mapSection.zoom = 0;
                    mapSection.scrollHorizontal = 0;
                    mapSection.scrollVertical = 0;
                    setLayerCentering(this.arrLayer, false);
                    //
                    string caption = "center="+map.visibleSection.center.ToString()+" / scale="+map.visibleSection.scale.ToString();
                    this.Text = caption;

                    //
                    System.Drawing.Image image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(map.image.rawImage));
                    pbxMap.Image = image;
                    pbxMap.Visible = true;
                    System.Drawing.Graphics gc = System.Drawing.Graphics.FromImage(pbxMap.Image);

                    gc.FillRectangle(BRUSH, 0, 0, width, BORDER);
                    gc.FillRectangle(BRUSH, 0, height - BORDER - 1, width, BORDER);
                    gc.FillRectangle(BRUSH, 0, 0, BORDER, height);
                    gc.FillRectangle(BRUSH, width - BORDER - 1, 0, BORDER, height);
                    gc.DrawRectangle(System.Drawing.Pens.Blue, 0, 0, width, BORDER);
                    gc.DrawRectangle(System.Drawing.Pens.Blue, 0, height - BORDER - 1, width, BORDER);
                    gc.DrawRectangle(System.Drawing.Pens.Blue, 0, 0, BORDER, height);
                    gc.DrawRectangle(System.Drawing.Pens.Blue, width - BORDER - 1, 0, BORDER, height);
                }
                
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }

        private void MapForm_SizeChanged(object sender, EventArgs e)
        {
            renderMap();
        }

        private void MapForm_ResizeEnd(object sender, EventArgs e)
        {
            renderMap();
        }

        private void MapForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            bool keyFound = true;
            if (e.KeyChar == '+')
                mapSection.zoom = 1;
            else if (e.KeyChar == '-')
                mapSection.zoom = -1;
            else if (e.KeyChar == '5')
                setLayerCentering(this.arrLayer, true);
            //            
            else if ((e.KeyChar == '1') || (e.KeyChar == '4') || (e.KeyChar == '7'))
                mapSection.scrollHorizontal = -SCROLL;
            else if ((e.KeyChar == '3') || (e.KeyChar == '6') || (e.KeyChar == '9'))
                mapSection.scrollHorizontal = SCROLL;
            //
            else if ((e.KeyChar == '1') || (e.KeyChar == '2') || (e.KeyChar == '3'))
                mapSection.scrollVertical = -SCROLL;
            else if ((e.KeyChar == '7') || (e.KeyChar == '8') || (e.KeyChar == '9'))
                mapSection.scrollVertical = SCROLL;
            else if ((e.KeyChar == 'x') || (e.KeyChar == 'X'))
            {
                switchLayer(Properties.Settings.Default.XYNLAYER_NAME);
            }
            else if ((e.KeyChar == 'a') || (e.KeyChar == 'A'))
            {
                switchLayer(Properties.Settings.Default.RESULTADDRESSLAYER_NAME);
            }
            else
                keyFound = false;

            if (keyFound)
                renderMap();
        }

        public void switchLayer(string name)
        {
            if (arrLayer != null)
            {
                foreach (Layer curLayer in arrLayer)
                {
                    if (curLayer.name == name)
                        curLayer.visible = !(curLayer.visible);
                }
            }
        }

        private void pbxMap_Click(object sender, EventArgs e)
        {
                PictureBox s = (PictureBox)sender;
                MouseEventArgs ea = (MouseEventArgs)e;
                // inner region: center clickpoint
                if ((BORDER < ea.X) && (ea.X < pbxMap.Width - BORDER) && (BORDER < ea.Y) && (ea.Y < pbxMap.Height - BORDER))
                {
                    mapSection.scrollHorizontal = 200 * (ea.X - s.Width / 2) / s.Width;
                    mapSection.scrollVertical = -200 * (ea.Y - s.Height / 2) / s.Height;
                }
                else
                {
                    if (ea.X<BORDER)
                        mapSection.scrollHorizontal = -SCROLL;
                    else if (pbxMap.Width - BORDER<ea.X)
                        mapSection.scrollHorizontal = SCROLL;
                    if ( ea.Y<BORDER)
                        mapSection.scrollVertical = SCROLL;
                    else if (pbxMap.Height - BORDER < ea.Y)
                        mapSection.scrollVertical = -SCROLL;
                }
                renderMap();
        }

        private void pbxMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Location == lastTooltip)
                return;
            else
                lastTooltip = e.Location;


            if (map != null && map.wrappedObjects != null)
            {
                List<string> lstDescr = new List<string>();
                LayerObject refLayerObject = null;
                ObjectInfos refObjectInfos = null;
                int refDist = int.MaxValue;
                foreach (ObjectInfos curObjectInfos in map.wrappedObjects)
                {
                    foreach (LayerObject curLayerObject in curObjectInfos.wrappedObjects)
                    {
                        int curX = Convert.ToInt32(curLayerObject.pixel.x);
                        int curY = Convert.ToInt32(curLayerObject.pixel.y);
                        int curDist = dist2(curX, curY, e.X, e.Y);
                        if ((curDist <= refDist) && (curDist < 50))
                        {
                            lstDescr.Add(curLayerObject.descr.Replace("$$", "\r\n") + " // " + curLayerObject.@ref.wkt);
                            refLayerObject = curLayerObject;
                            refDist = curDist;
                            refObjectInfos = curObjectInfos;
                        }
                    }
                }
                if (lstDescr.Count > 0)
                {
                    string label = lstDescr.Count.ToString() + " hit" + ((lstDescr.Count > 1) ? ("s") : (""));
                    //System.Drawing.Graphics gc = System.Drawing.Graphics.FromImage(pbxMap.Image);

                    int x = Convert.ToInt16(refLayerObject.pixel.x), y = Convert.ToInt16(refLayerObject.pixel.y);
                    toolTip1.Active = true;
                    toolTip1.ShowAlways = true;
                    toolTip1.ToolTipTitle = label;
                    string toolString = String.Join("\r\n", lstDescr.ToArray());
                    toolTip1.Show(toolString, pbxMap, x, y, 100000);
                    //gc.DrawRectangle(System.Drawing.Pens.Black, new Rectangle(x - 8, y - 8, 17, 17));
                    //gc.DrawLine(System.Drawing.Pens.Black, x - 8, y - 8, x + 8, y + 8);
                    //gc.DrawLine(System.Drawing.Pens.Black, x - 8, y + 8, x + 8, y - 8);
                    //System.Drawing.Bitmap bmp = new System.Drawing.Bitmap("C:\\PTV-XServer\\Server\\xmap-1.8.1.4\\data\\bitmaps\\location_rough.bmp");
                    //bmp.MakeTransparent(System.Drawing.Color.Green);
                    //gc.DrawImage(bmp, x, y);
                    pbxMap.Update();
                }
                else
                {
                    toolTip1.Hide(this);
                    //this.Text = "";
                }
            }
        }

        private int dist2(int x1,int y1,int x2,int y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
        }

        private void pbxMap_MouseEnter(object sender, EventArgs e)
        {
            //pbxMap.Focus();
        }

    }
}