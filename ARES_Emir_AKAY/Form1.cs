using Microsoft.Web.WebView2.WinForms;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using static MAVLink;

namespace ARES_Emir_AKAY
{
    public partial class Form1 : Form
    {
        private float? previousSpeed = null;
        private DateTime previousSpeedTime = DateTime.MinValue;
        private System.Windows.Forms.Timer accelerationUpdateTimer;

        private readonly HttpClient httpClient = new HttpClient();

        private float? lastRelativeAltitude = null;

        private System.Windows.Forms.Timer statusUpdateInternalTimer;

        private bool isReconnecting = false;

        private System.Windows.Forms.Timer mapUpdateTimer;

        private SerialPort serialPort;
        private byte systemId = 255;
        private byte componentId = 190;
        private byte seq = 0;
        private string selectedPort = "COM7";
        private bool heartbeatReceived = false;
        private System.Windows.Forms.Timer connectionCheckTimer;
        private System.Windows.Forms.Timer statusUpdateTimer;
        private System.Windows.Forms.Timer gpsUpdateTimer;
        private System.Windows.Forms.Timer speedUpdateTimer;
        private System.Windows.Forms.Timer attitudeUpdateTimer;
        private System.Windows.Forms.Timer altitudeUpdateTimer;
        private System.Windows.Forms.Timer gpsTimeUpdateTimer;

        private string currentConnectionStatus = "";
        private string lastLatitude = "GPS alýnamadý";
        private string lastLongitude = "GPS alýnamadý";
        private float? lastGroundSpeed = null;
        private DateTime lastSpeedTime;

        private float? lastYaw = null;
        private float? lastPitch = null;
        private float? lastRoll = null;

        private float? lastAltitude = null;
        private float? referenceAltitude = null;
        private ulong? lastGpsTime = null;

        private DateTime lastAttitudeUpdateTime = DateTime.MinValue;
        private DateTime lastAltitudeUpdateTime = DateTime.MinValue;
        private DateTime lastGpsTimeUpdateTime = DateTime.MinValue;
        private DateTime lastGpsUpdateTime = DateTime.MinValue;

        private WebView2 mapView;

        public Form1()
        {
            InitializeComponent();
            StartStatusUpdateTimer();
            StartGpsUpdateTimer();
            StartSpeedUpdateTimer();
            StartAttitudeUpdateTimer();
            StartAltitudeUpdateTimer();
            StartGpsTimeUpdateTimer();
            StartAccelerationUpdateTimer();
        }

        private async void cameraUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var data = await httpClient.GetByteArrayAsync("http://localhost:5000/video");

                using (MemoryStream ms = new MemoryStream(data))
                {
                    Image image = Image.FromStream(ms);

                    if (pictureBox4.Image != null)
                        pictureBox4.Image.Dispose();

                    pictureBox4.Image = image;
                    pictureBox4.BackColor = Color.Black;
                }
            }
            catch (Exception)
            {
                pictureBox4.BackColor = Color.White;
            }
        }

        private void StartMapUpdateTimer1(WebView2 view)
        {
            mapUpdateTimer = new System.Windows.Forms.Timer();
            mapUpdateTimer.Interval = 200; // 5 kez/sn
            mapUpdateTimer.Tick += (s, e) =>
            {
                if (view?.CoreWebView2 != null &&
                    lastLatitude != "GPS alýnamadý" &&
                    lastLongitude != "GPS alýnamadý")
                {
                    if (double.TryParse(lastLatitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(lastLongitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                    {
                        string script = $"updatePosition({lat.ToString("F7", CultureInfo.InvariantCulture)}, {lon.ToString("F7", CultureInfo.InvariantCulture)});";
                        view.CoreWebView2.ExecuteScriptAsync(script);
                    }
                }
            };
            mapUpdateTimer.Start();
        }

        private void StartStatusUpdateTimer()
        {
            statusUpdateTimer = new System.Windows.Forms.Timer();
            statusUpdateTimer.Interval = 200;
            statusUpdateTimer.Tick += (s, e) =>
            {
                textBox10.Text = currentConnectionStatus;
            };
            statusUpdateTimer.Start();
        }

        private void StartGpsUpdateTimer()
        {
            gpsUpdateTimer = new System.Windows.Forms.Timer();
            gpsUpdateTimer.Interval = 200;
            gpsUpdateTimer.Tick += (s, e) =>
            {
                if ((DateTime.Now - lastGpsUpdateTime).TotalSeconds < 3)
                {
                    textBox9.Text = lastLatitude;
                    textBox17.Text = lastLongitude;
                }
                else
                {
                    textBox9.Text = "veri alýnamadý";
                    textBox17.Text = "veri alýnamadý";
                }
            };
            gpsUpdateTimer.Start();
        }

        private void StartSpeedUpdateTimer()
        {
            speedUpdateTimer = new System.Windows.Forms.Timer();
            speedUpdateTimer.Interval = 200;
            speedUpdateTimer.Tick += (s, e) =>
            {
                if (lastGroundSpeed.HasValue && (DateTime.Now - lastSpeedTime).TotalSeconds < 3)
                    textBox15.Text = $"{lastGroundSpeed.Value:F2} m/s";
                else
                    textBox15.Text = "veri alýnamadý";
            };
            speedUpdateTimer.Start();
        }

        private void StartAttitudeUpdateTimer()
        {
            attitudeUpdateTimer = new System.Windows.Forms.Timer();
            attitudeUpdateTimer.Interval = 200;
            attitudeUpdateTimer.Tick += (s, e) =>
            {
                TimeSpan elapsed = DateTime.Now - lastAttitudeUpdateTime;
                if (elapsed.TotalSeconds < 3)
                {
                    textBox4.Text = lastYaw.HasValue ? $"{lastYaw.Value:F2}°" : "veri alýnamadý";
                    textBox2.Text = lastPitch.HasValue ? $"{lastPitch.Value:F2}°" : "veri alýnamadý";
                    textBox3.Text = lastRoll.HasValue ? $"{lastRoll.Value:F2}°" : "veri alýnamadý";
                }
                else
                {
                    textBox4.Text = "veri alýnamadý";
                    textBox2.Text = "veri alýnamadý";
                    textBox3.Text = "veri alýnamadý";
                }
            };
            attitudeUpdateTimer.Start();
        }

        private void StartAltitudeUpdateTimer()
        {
            altitudeUpdateTimer = new System.Windows.Forms.Timer();
            altitudeUpdateTimer.Interval = 200;
            altitudeUpdateTimer.Tick += (s, e) =>
            {
                if (DateTime.Now - lastAltitudeUpdateTime < TimeSpan.FromSeconds(3) &&
                    lastRelativeAltitude.HasValue)
                {
                    textBox14.Text = $"{lastRelativeAltitude.Value:F2} m";
                }
                else
                {
                    textBox14.Text = "veri alýnamadý";
                }
            };
            altitudeUpdateTimer.Start();
        }

        private void StartGpsTimeUpdateTimer()
        {
            gpsTimeUpdateTimer = new System.Windows.Forms.Timer();
            gpsTimeUpdateTimer.Interval = 200;
            gpsTimeUpdateTimer.Tick += (s, e) =>
            {
                if (lastGpsTime.HasValue && (DateTime.Now - lastGpsTimeUpdateTime).TotalSeconds < 3)
                {
                    DateTime gpsDate = new DateTime(1970, 1, 1).AddMilliseconds(lastGpsTime.Value).ToLocalTime();
                    textBox1.Text = gpsDate.ToString("HH:mm:ss");
                }
                else
                {
                    textBox1.Text = "veri alýnamadý";
                }
            };
            gpsTimeUpdateTimer.Start();
        }

        private void OnSpeedDataReceived(float groundspeed)
        {
            lastGroundSpeed = groundspeed;
            lastSpeedTime = DateTime.Now;
        }

        private async Task panelHaritaHaritaYukle()
        {
            panelHarita.Controls.Clear();
            mapView = new WebView2();
            mapView.Dock = DockStyle.Fill;
            panelHarita.Controls.Add(mapView);

            await mapView.EnsureCoreWebView2Async();
            mapView.CoreWebView2.NavigateToString(GetHtml());
            StartMapUpdateTimer1(mapView);
        }

        private void StartAccelerationUpdateTimer()
        {
            accelerationUpdateTimer = new System.Windows.Forms.Timer();
            accelerationUpdateTimer.Interval = 200;
            accelerationUpdateTimer.Tick += (s, e) =>
            {
                if (lastGroundSpeed.HasValue)
                {
                    DateTime now = DateTime.Now;
                    if (previousSpeed.HasValue)
                    {
                        double timeDelta = (now - previousSpeedTime).TotalSeconds;
                        if (timeDelta > 0)
                        {
                            float acceleration = (lastGroundSpeed.Value - previousSpeed.Value) / (float)timeDelta;
                            textBox14.Text = $"{acceleration:F2} m/s²";
                        }
                        else
                        {
                            textBox14.Text = "0.00 m/s²";
                        }
                    }

                    previousSpeed = lastGroundSpeed;
                    previousSpeedTime = now;
                }
                else
                {
                    textBox14.Text = "veri alýnamadý";
                }
            };
            accelerationUpdateTimer.Start();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            cameraUpdateTimer.Interval = 33;
            cameraUpdateTimer.Tick += cameraUpdateTimer_Tick;

            this.TopMost = true;

            // BAÐLANTI baþlýðý
            Label veriLabel = new Label()
            {
                Text = " BAÐLANTI",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Gainsboro,
                AutoSize = true,
                Location = new Point(100, 180),
                BackColor = Color.Black
            };
            this.Controls.Add(veriLabel);

            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, 20, 20, 180, 90);
            path.AddArc(button1.Width - 20, 0, 20, 20, 270, 90);
            path.AddArc(button1.Width - 20, button1.Height - 20, 20, 20, 0, 90);
            path.AddArc(0, button1.Height - 20, 20, 20, 90, 90);
            path.CloseFigure();
            button1.Region = new Region(path);

            GraphicsPath path1 = new GraphicsPath();
            path1.AddArc(0, 0, 20, 20, 180, 90);
            path1.AddArc(button2.Width - 20, 0, 20, 20, 270, 90);
            path1.AddArc(button2.Width - 20, button2.Height - 20, 20, 20, 0, 90);
            path1.AddArc(0, button2.Height - 20, 20, 20, 90, 90);
            path1.CloseFigure();
            button2.Region = new Region(path1);

            GraphicsPath path2 = new GraphicsPath();
            path2.AddArc(0, 0, 20, 20, 180, 90);
            path2.AddArc(button3.Width - 20, 0, 20, 20, 270, 90);
            path2.AddArc(button3.Width - 20, button3.Height - 20, 20, 20, 0, 90);
            path2.AddArc(0, button3.Height - 20, 20, 20, 90, 90);
            path2.CloseFigure();
            button3.Region = new Region(path2);

            GraphicsPath path3 = new GraphicsPath();
            path3.AddArc(0, 0, 20, 20, 180, 90);
            path3.AddArc(button4.Width - 20, 0, 20, 20, 270, 90);
            path3.AddArc(button4.Width - 20, button4.Height - 20, 20, 20, 0, 90);
            path3.AddArc(0, button4.Height - 20, 20, 20, 90, 90);
            path3.CloseFigure();
            button4.Region = new Region(path3);

            Panel leftLine = new Panel()
            {
                Location = new Point(veriLabel.Left - 100, veriLabel.Top + veriLabel.Height / 2),
                Size = new Size(120, 1),
                BackColor = Color.Gray
            };
            this.Controls.Add(leftLine);

            Panel rightLine = new Panel()
            {
                Location = new Point(veriLabel.Right + 10, veriLabel.Top + veriLabel.Height / 2),
                Size = new Size(120, 1),
                BackColor = Color.Gray
            };
            this.Controls.Add(rightLine);

            Panel ustLine = new Panel()
            {
                Location = new Point(329, 0),
                Size = new Size(1, 1000),
                BackColor = Color.Gray
            };
            this.Controls.Add(ustLine);

            Label veri2Label = new Label()
            {
                Text = " ",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Gainsboro,
                AutoSize = true,
                Location = new Point(650, 50),
                BackColor = Color.Black
            };
            this.Controls.Add(veri2Label);

            int lineY = veri2Label.Top + veri2Label.Height / 2;

            Panel left2Line = new Panel()
            {
                Location = new Point(veri2Label.Left - 320, lineY),
                Size = new Size(320, 1),
                BackColor = Color.Gray
            };
            this.Controls.Add(left2Line);

            Panel right2Line = new Panel()
            {
                Location = new Point(veri2Label.Right + 10, lineY),
                Size = new Size(350, 1),
                BackColor = Color.Gray
            };
            this.Controls.Add(right2Line);

            Panel ust2Line = new Panel()
            {
                Location = new Point(1089, 0),
                Size = new Size(1, 1000),
                BackColor = Color.Gray
            };
            this.Controls.Add(ust2Line);

            Panel altLine = new Panel()
            {
                Location = new Point(0, 600),
                Size = new Size(330, 1),
                BackColor = Color.Gray
            };
            this.Controls.Add(altLine);

            Panel alt2Line = new Panel()
            {
                Location = new Point(0, 905),
                Size = new Size(330, 1),
                BackColor = Color.Gray
            };
            this.Controls.Add(alt2Line);

            Panel yanLine = new Panel()
            {
                Location = new Point(1915, 0),
                Size = new Size(1, 1000),
                BackColor = Color.Gray
            };
            this.Controls.Add(yanLine);

            statusUpdateInternalTimer = new System.Windows.Forms.Timer();
            statusUpdateInternalTimer.Interval = 200;
            statusUpdateInternalTimer.Tick += (s, e2) =>
            {
                textBox10.Text = currentConnectionStatus;
            };
            statusUpdateInternalTimer.Start();

            await panelHaritaHaritaYukle();
        }

        // =================== HARÝTA HTML (Çokgen + Çember + Marker) ===================
        private string GetHtml()
        {
            // C# verbatim string (@"...") içinde " karakteri "" olarak escape edilmelidir.
            return @"<!doctype html>
<html lang='tr'>
<head>
  <meta charset='utf-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1' />
  <title>ARES Harita Sistemi (GCS Entegre)</title>
  <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' crossorigin='' />
  
  <script src=""https://rawcdn.githack.com/bbecquet/Leaflet.RotatedMarker/master/leaflet.rotatedMarker.js""></script> 

  <style>
    :root{ --bg:#0b1220; --card:#111a2b; --muted:#7c869a; --accent:#3b82f6; --ok:#22c55e; --err:#ef4444; --text:#e5e7eb; }
    html,body{height:100%; margin:0; font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Arial; background:var(--bg); color:var(--text)}
    .wrap{display:grid; grid-template-columns: 360px 1fr; gap:16px; height:100%;}
    .sidebar{padding:16px; background:var(--card); box-shadow: 0 0 0 1px rgba(255,255,255,0.04) inset; overflow:auto}
    .sidebar h1{font-size:18px; margin:0 0 8px} [cite: 12]
    .sidebar p{color:var(--muted); font-size:13px; line-height:1.3}
    .field{margin-top:12px}
    .field label{display:block; font-size:12px; color:var(--muted); margin-bottom:6px} [cite: 13]
    textarea, input[type='text']{width:100%; box-sizing:border-box; background:#0e172a; color:var(--text); border:1px solid #1f2937; border-radius:12px; padding:10px; font-size:13px} [cite: 13, 14]
    textarea{min-height:92px; resize:vertical} [cite: 14]
    .row{display:flex; gap:8px}
    button{appearance:none; border:1px solid #1f2937; background:#0e172a; color:var(--text); padding:10px 12px; border-radius:12px; cursor:pointer; font-weight:600} [cite: 14, 15]
    button.primary{background:var(--accent); border-color:var(--accent); color:white} [cite: 15]
    button.ghost{background:transparent} [cite: 15]
    button:disabled{opacity:.6; cursor:not-allowed} [cite: 15, 16]
    .hint{font-size:12px; color:var(--muted)} [cite: 16]
    .ok{color:var(--ok)} .err{color:var(--err)} [cite: 16]
    #map{height:100%; border-left: 1px solid #0f172a} [cite: 16, 17]
    details{margin-top:10px} [cite: 17]
    summary{cursor:pointer; color:#93c5fd} [cite: 17]
    .footer{margin-top:12px; font-size:12px; color:var(--muted)} [cite: 17, 18]
    @media (max-width: 900px){ .wrap{grid-template-columns: 1fr; grid-template-rows: auto 1fr} .sidebar{border-bottom:1px solid #0f172a} } [cite: 18]

    /* Python Haritasýndan Eklendi: Waypoint Label Stili */
    .waypoint-label { 
        background: transparent !important;
        border: none !important;
        box-shadow: none !important;
        color: white !important;
        font-weight: bold !important;
        font-size: 12px !important;
    }
  </style>
</head>
<body>
  <div class='wrap'>
    <aside class='sidebar'>
      <h1>ARES Harita Sistemi</h1>
      <p>Koordinatlarla çokgen (alan) ve yarýçaplý çember(ler) çizebilirsiniz. [cite: 19] Sol kutulara deðerleri yazýp <b>Çiz</b>e týklayýn.</p> [cite: 19]

      <div class='field'>
        <label>Çokgen koordinatlarý (lat,lng — her satýra bir nokta)</label>
        <textarea id='polyInput' placeholder='41.015, 28.979
41.02, 28.99
41.01, 29.0'></textarea> [cite: 19]
        <div class='hint'>Ondalýk ayýrýcý olarak '.' [cite: 19] kullanýn. Virgül ',' lat/lng ayýrýcýdýr.</div> [cite: 20]
      </div>

      <div class='field'>
        <label>Çember(ler) (lat,lng,radius — satýr baþýna bir çember)</label>
        <textarea id='circleInput' placeholder='41.0082, 28.9784, 500m
41.0150, 28.9900, 1km'></textarea> [cite: 20]
        <div class='hint'>Yarýçap <code>m</code> veya <code>km</code> olabilir. [cite: 20] (Örn. <code>750</code> = 750 m)</div> [cite: 21]
      </div>

      <div class='field row'>
        <button class='primary' id='drawBtn'>Çiz</button> [cite: 21]
        <button id='clearBtn'>Temizle</button> [cite: 21]
        <button class='ghost' id='sampleBtn'>Örnekleri Doldur</button> [cite: 21]
      </div>

      <details>
        <summary>Format örnekleri & ipuçlarý</summary>
        <div class='hint' style='margin-top:8px'>
          <b>Çokgen:</b><br/>41.015, 28.979<br/>41.020, 28.990<br/>41.010, 29.000 [cite: 21, 22]
          <br/><br/><b>Çember:</b><br/>41.0082, 28.9784, 500m<br/>41.0150, 28.9900, 1km [cite: 22]
          <br/><br/>Hatalý satýrlar atlanýr ve uyarý verilir. [cite: 22]
        </div> [cite: 23]
      </details>

      <div id='status' class='footer'></div> [cite: 23]
    </aside>

    <main id='map'></main>
  </div>

  <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js' crossorigin=''></script>
  <script>
    // --- Harita Kurulumu ---
    const map = L.map('map', { zoomControl: true }); [cite: 23]
    
    // Python'daki Google Uydu Katmaný ile deðiþtirildi
    L.tileLayer('https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}', { 
        maxZoom: 20,
        attribution: '&copy; Google Maps'
    }).addTo(map);

    L.control.scale({ metric:true, imperial:false }).addTo(map); [cite: 25]

    // --- Python Haritasýndan Eklendi: Sýnýr Poligonu ---
    var boundaryCoords = [ [cite: 2]
        [40.19891, 25.88131],
        [40.20009, 25.87654],
        [40.20727, 25.87931],
        [40.20612, 25.88425],
        [40.19891, 25.88131]
    ];
    var boundary = L.polygon(boundaryCoords, { [cite: 3]
        color: 'red',
        weight: 3,
        opacity: 0.8,
        fillColor: 'red',
        fillOpacity: 0.0
    }).addTo(map);
    map.fitBounds(boundary.getBounds()); // Haritayý sýnýrlara odakla [cite: 4]


    // --- C# Haritasýndaki Çizim Katmaný ---
    const drawn = L.featureGroup().addTo(map); [cite: 25]

    // --- Python Haritasýndan Eklendi: Geliþmiþ ÝHA Ýkonu ---
    const planeIcon = L.icon({ [cite: 4]
      iconUrl: 'file:///C:/Users/Emre/Desktop/planetracker4.png', // Python'daki ikon yolu [cite: 4]
      iconSize: [28, 28], [cite: 4]
      iconAnchor: [14, 14] [cite: 4]
    });
    const posMarker = L.marker([39.0, 35.0], { [cite: 5, 26]
        icon: planeIcon,
        rotationAngle: 0, // Dönüþ desteði eklendi 
        rotationOrigin: 'center' 
    }).addTo(map);

    // Python'daki 'updateUAV' fonksiyonu ile 'updatePosition' güncellendi
    function updateUAV(lat, lon, heading, center){ [cite: 9]
      if(!posMarker) return;
      posMarker.setLatLng([lat, lon]);
      posMarker.setRotationAngle(heading); // Dönüþ açýsýný ayarla [cite: 9]
      if(center) map.setView([lat,lon], map.getZoom()); // Python'daki gibi setView kullanýldý [cite: 9]
    }
    window.updateUAV = updateUAV; // Global olarak eriþilebilir yap [cite: 10]

    
    // --- Python Haritasýndan Eklendi: Waypoint Deðiþkenleri ve C# Bridge ---
    window.targetMarker = null; [cite: 6]
    window.handler = null; [cite: 6]

    // C# (.NET) Bridge (Python'daki QWebChannel'dan uyarlandý)
    // C# tarafý (örn. WebView2) yüklendiðinde ""handler"" adýnda bir host object inject etmelidir.
    // Örnek C#: await webView.CoreWebView2.AddHostObjectToScriptAsync(""handler"", new HostHandler());
    // (HostHandler C# sýnýfý NewTarget(double lat, double lon) metodunu içermelidir)
    if (window.chrome && window.chrome.webview) {
        try {
            // .NET 8 ve sonrasý için:
            // window.handler = window.chrome.webview.hostObjects.handler;
            
            // .NET 6/7 (sync) için:
            window.chrome.webview.hostObjects.sync().then(function() {
                window.handler = window.chrome.webview.hostObjects.handler;
                console.log('C# handler object registered.');
            });
        } catch(e) {
            console.error('C# handler object could not be registered. \'handler\' adýnda bir HostObject eklediðinizden emin olun.', e);
        }
    } else {
        console.warn('WebView2 bridge (window.chrome.webview) not found. C#-JS iletiþimi (Waypoint) çalýþmayabilir.');
    }

    // --- Python Haritasýndan Eklendi: Harita Týklama (Waypoint) ---
    map.on('click', function(e) { [cite: 7]
        var lat = e.latlng.lat;
        var lon = e.latlng.lng;
        console.log('?? Harita týklandý:', lat, lon);

        // Eski marker'ý kaldýr
        if(window.targetMarker){ [cite: 7]
            map.removeLayer(window.targetMarker);
        }

        // Yeni waypoint marker
        window.targetMarker = L.circleMarker([lat, lon], { [cite: 7]
            color: 'white',
            fillColor: 'red',
            fillOpacity: 1,
            radius: 10, [cite: 8]
            weight: 2 [cite: 8]
        }).addTo(map);

        // WP yazýsý ekle
        window.targetMarker.bindTooltip('WP', { [cite: 8]
            permanent: true,
            direction: 'center',
            className: 'waypoint-label'
        });

        console.log('? Waypoint eklendi:', lat, lon);

        // C# tarafýna bildir
        if(window.handler && window.handler.NewTarget){ // C# metotlarý genelde PascalCase (NewTarget) olur [cite: 8]
            window.handler.NewTarget(lat, lon);
        } else {
             if(window.handler && window.handler.newTarget) { // Python/Qt stili (lowercase) [cite: 8]
                window.handler.newTarget(lat, lon);
             } else {
                console.warn('window.handler.NewTarget veya .newTarget bulunamadý. C# tarafýna bildirim yapýlamadý.');
             }
        }
    });


    // --- C# Haritasýndaki Orijinal Çizim Fonksiyonlarý ---
    const polyInput   = document.getElementById('polyInput'); [cite: 27]
    const circleInput = document.getElementById('circleInput'); [cite: 28]
    const drawBtn     = document.getElementById('drawBtn'); [cite: 28]
    const clearBtn    = document.getElementById('clearBtn'); [cite: 28]
    const sampleBtn   = document.getElementById('sampleBtn'); [cite: 29]
    const statusEl    = document.getElementById('status'); [cite: 29]

    function setStatus(msg, ok=false){ [cite: 30]
      statusEl.innerHTML = ok ? `<span style='color:#22c55e'>${msg}</span>` : `<span style='color:#ef4444'>${msg}</span>`; [cite: 30]
    }

    function parseLatLng(line){ [cite: 31]
      const parts = line.split(',').map(s => s.trim()).filter(Boolean); [cite: 31]
      if(parts.length < 2) return null; [cite: 32]
      const lat = Number(parts[0].replace(/\s+/g,''));
      const lng = Number(parts[1].replace(/\s+/g,'')); [cite: 32]
      if(Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat) <= 90 && Math.abs(lng) <= 180){ [cite: 33]
        return [lat, lng]; [cite: 33]
      }
      return null; [cite: 34]
    }

    function parsePolygon(text){ [cite: 34]
      const lines = text.split(/\n|;|\r/).map(s => s.trim()).filter(Boolean); [cite: 34]
      const coords = []; const bad = []; [cite: 35]
      lines.forEach((ln, i) => {
        const ll = parseLatLng(ln);
        if(ll) coords.push(ll); else bad.push(i+1); [cite: 35]
      });
      return { coords, bad }; [cite: 36]
    }

    function parseMeters(val){ [cite: 36]
      const s = String(val).trim().toLowerCase();
      if(s.endsWith('km')) return parseFloat(s) * 1000; [cite: 37]
      if(s.endsWith('m'))  return parseFloat(s); [cite: 37]
      return parseFloat(s); [cite: 37]
    }

    function parseCircles(text){ [cite: 38]
      const lines = text.split(/\n|;|\r/).map(s => s.trim()).filter(Boolean); [cite: 38]
      const items = []; const bad = []; [cite: 39]
      lines.forEach((ln, i) => {
        const parts = ln.split(',').map(s => s.trim()).filter(Boolean);
        if(parts.length < 3){ bad.push(i+1); return; } [cite: 39]
        const ll = parseLatLng(parts.slice(0,2).join(','));
        const r = parseMeters(parts[2]);
        if(ll && Number.isFinite(r) && r > 0){ items.push({ center: ll, radius: r }); } else { bad.push(i+1); } [cite: 39]
      });
      return { items, bad }; [cite: 40]
    }

    function draw(){ [cite: 40]
      drawn.clearLayers(); [cite: 40]
      let any = false; let messages = []; [cite: 41]

      const { coords, bad: badPoly } = parsePolygon(polyInput.value); [cite: 41]
      if(coords.length >= 3){ [cite: 42]
        L.polygon(coords, { color:'#60a5fa', weight:2, fillColor:'#3b82f6', fillOpacity:0.25 }).addTo(drawn); [cite: 42]
        any = true;
      } else if(polyInput.value.trim().length){ [cite: 43]
        messages.push(`Çokgen için en az 3 nokta gerekli (geçerli satýr sayýsý: ${coords.length}).`); [cite: 43]
      }
      if(badPoly.length){ messages.push(`Çokgen: atlanan satýrlar ? ${badPoly.join(', ')}`); [cite: 44]
      }

      const { items, bad: badCircles } = parseCircles(circleInput.value); [cite: 45]
      items.forEach(c => { [cite: 46]
        L.circle(c.center, { radius: c.radius, color:'#22c55e', weight:2, fillOpacity:0.15 }).addTo(drawn); [cite: 46]
        any = true;
      });
      if(badCircles.length){ messages.push(`Çember(ler): atlanan satýrlar ? ${badCircles.join(', ')}`); } [cite: 47]

      if(!any){ setStatus(messages.join('<br/>') || 'Çizilecek bir þey bulunamadý.'); [cite: 47]
      return; } [cite: 48]

      try{
        const b = drawn.getBounds(); [cite: 48]
        if(b.isValid()) map.fitBounds(b.pad(0.2)); [cite: 49]
      }catch(e){}

      setStatus('Çizim tamamlandý.', true); [cite: 49]
    }

    drawBtn.addEventListener('click', draw); [cite: 49]
    clearBtn.addEventListener('click', () => { drawn.clearLayers(); setStatus('Temizlendi.', true); }); [cite: 50]
    sampleBtn.addEventListener('click', () => { [cite: 50]
      polyInput.value = '41.0150, 28.9790\n41.0250, 28.9900\n41.0100, 29.0050';
      circleInput.value = '41.0082, 28.9784, 500m\n41.0150, 28.9900, 1km';
      setStatus('Örnekler yüklendi. Çiz için týklayýn.', true);
    });
    document.addEventListener('keydown', (e) => { [cite: 51]
      if((e.ctrlKey || e.metaKey) && e.key === 'Enter') draw();
    });
    
    // --- .NET köprüleri (Orijinal C# dosyasýndan) ---
    window.setShapes = function(polyText, circleText){ [cite: 52]
      try{
        polyInput.value   = (polyText   ?? '').toString(); [cite: 52]
        circleInput.value = (circleText ?? '').toString(); [cite: 53]
        draw();
        return true; [cite: 53]
      }catch(e){
        setStatus('Þekiller yüklenirken hata oluþtu: ' + (e?.message||e), false);
        return false; [cite: 54]
      }
    };
    window.clearShapes = function(){ [cite: 54]
      try{ drawn.clearLayers(); setStatus('Temizlendi.', true); [cite: 54]
      return true; }catch(e){ return false; } [cite: 55]
    };
  </script>
</body>
</html>";
        }

        // =================== /HARÝTA HTML ===================

        // .NET tarafý için programlý çizim yardýmcýlarý
        private void DrawShapes(string polygonText, string circlesText)
        {
            if (mapView?.CoreWebView2 == null) return;

            string polyJson = JsonSerializer.Serialize(polygonText ?? "");
            string circleJson = JsonSerializer.Serialize(circlesText ?? "");
            string js = $"window.setShapes && window.setShapes({polyJson}, {circleJson});";
            mapView.CoreWebView2.ExecuteScriptAsync(js);
        }

        private void ClearShapes()
        {
            if (mapView?.CoreWebView2 == null) return;
            mapView.CoreWebView2.ExecuteScriptAsync("window.clearShapes && window.clearShapes();");
        }

        private void label6_Click(object sender, EventArgs e) { }
        private void textBox18_TextChanged(object sender, EventArgs e) { selectedPort = textBox18.Text; }

        private void button1_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            btn.Enabled = false;
            currentConnectionStatus = "Baðlanýyor...";
            heartbeatReceived = false;
            Task.Run(() => TryConnect(btn));
        }

        private void TryConnect(Button? btn = null)
        {
            try
            {
                serialPort = new SerialPort(selectedPort, 57600, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();
                SendHeartbeat();

                int timeout = 10000;
                int waited = 0;
                while (!heartbeatReceived && waited < timeout)
                {
                    Task.Delay(200).Wait();
                    waited += 200;
                }

                if (heartbeatReceived)
                {
                    Invoke(new Action(() =>
                    {
                        currentConnectionStatus = "Baðlantý baþarýlý";
                        StartConnectionMonitor();
                    }));
                }
                else
                {
                    Invoke(new Action(() => currentConnectionStatus = "Baðlantý zaman aþýmý"));
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    currentConnectionStatus = "Baðlantý hatasý";
                    MessageBox.Show(ex.Message, "Baðlantý Hatasý");  // *********** ÖNEMLÝ GÖRSEL GÜZELLÝK 
                }));
            }
            finally
            {
                if (btn != null)
                {
                    Invoke(new Action(() => btn.Enabled = true));
                }
            }
        }

        private void StartConnectionMonitor()
        {
            if (connectionCheckTimer != null)
            {
                connectionCheckTimer.Stop();
                connectionCheckTimer.Dispose();
            }

            connectionCheckTimer = new System.Windows.Forms.Timer();
            connectionCheckTimer.Interval = 1000;
            connectionCheckTimer.Tick += (s, e) =>
            {
                if (serialPort?.IsOpen == true)
                {
                    currentConnectionStatus = "Baðlantý aktif";
                    SendHeartbeat();
                }
                else
                {
                    currentConnectionStatus = "Yeniden baðlanýlýyor...";
                    heartbeatReceived = false;
                    TryReconnect();
                }
            };
            connectionCheckTimer.Start();
        }

        private void TryReconnect()
        {
            if (isReconnecting) return;
            isReconnecting = true;

            Task.Run(() =>
            {
                try
                {
                    Invoke(new Action(() => currentConnectionStatus = "Yeniden baðlanma deneniyor..."));

                    if (serialPort != null)
                    {
                        try
                        {
                            if (serialPort.IsOpen)
                                serialPort.Close();
                        }
                        catch { }

                        serialPort.Dispose();
                        serialPort = null;
                    }

                    serialPort = new SerialPort(selectedPort, 57600, Parity.None, 8, StopBits.One);
                    serialPort.DataReceived += SerialPort_DataReceived;
                    serialPort.Open();
                    SendHeartbeat();

                    int timeout = 5000;
                    int waited = 0;
                    heartbeatReceived = false;

                    while (!heartbeatReceived && waited < timeout)
                    {
                        Task.Delay(200).Wait();
                        waited += 200;
                    }

                    if (heartbeatReceived)
                    {
                        Invoke(new Action(() =>
                        {
                            currentConnectionStatus = "Yeniden baðlantý baþarýlý";
                            StartConnectionMonitor();
                        }));
                    }
                    else
                    {
                        Invoke(new Action(() =>
                        {
                            currentConnectionStatus = "Baðlantý tekrar baþarýsýz";
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        currentConnectionStatus = "Yeniden baðlantý hatasý";
                        MessageBox.Show("Yeniden baðlantý hatasý: " + ex.Message);
                    }));
                }
                finally
                {
                    isReconnecting = false;
                }
            });
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                serialPort.Read(buffer, 0, bytesToRead);

                Console.WriteLine("Gelen ham veri: " + BitConverter.ToString(buffer));

                for (int idx = 0; idx < buffer.Length;)
                {
                    byte header = buffer[idx];

                    if ((header == 0xFE && idx + 6 < buffer.Length) || (header == 0xFD && idx + 10 < buffer.Length))
                    {
                        byte len = buffer[idx + 1];
                        int headerSize = (header == 0xFE) ? 6 : 10; // MAVLink v1: 6, v2: 10
                        int msgIdOffset = (header == 0xFE) ? 5 : 7;
                        int payloadIndex = idx + headerSize;

                        if (payloadIndex + len > buffer.Length)
                        {
                            break;
                        }

                        uint msgId = (header == 0xFE)
                            ? buffer[idx + msgIdOffset]
                            : (uint)(buffer[idx + 7] | (buffer[idx + 8] << 8) | (buffer[idx + 9] << 16));

                        Console.WriteLine($"[DEBUG] Gelen mesaj ID: {msgId}");

                        if (msgId == 0)
                        {
                            heartbeatReceived = true;
                        }
                        else if (msgId == 24) // GPS_RAW_INT
                        {
                            ulong time_usec = BitConverter.ToUInt64(buffer, payloadIndex + 0);
                            int lat = BitConverter.ToInt32(buffer, payloadIndex + 8);
                            int lon = BitConverter.ToInt32(buffer, payloadIndex + 12);
                            int alt = BitConverter.ToInt32(buffer, payloadIndex + 16);

                            double latitude = lat / 1e7;
                            double longitude = lon / 1e7;
                            float altitude = alt / 1000f;

                            lastLatitude = latitude.ToString("F7", CultureInfo.InvariantCulture);
                            lastLongitude = longitude.ToString("F7", CultureInfo.InvariantCulture);
                            lastAltitude = altitude;
                            lastGpsTime = time_usec / 1000;

                            if (!referenceAltitude.HasValue)
                                referenceAltitude = altitude;

                            lastGpsUpdateTime = DateTime.Now;
                            lastGpsTimeUpdateTime = DateTime.Now;
                        }
                        else if (msgId == 74) // VFR_HUD
                        {
                            float groundspeed = BitConverter.ToSingle(buffer, payloadIndex + 4);
                            OnSpeedDataReceived(groundspeed);

                            lastSpeedTime = DateTime.Now;
                        }
                        else if (msgId == 30) // ATTITUDE
                        {
                            lastRoll = BitConverter.ToSingle(buffer, payloadIndex + 0) * (180f / (float)Math.PI);
                            lastPitch = BitConverter.ToSingle(buffer, payloadIndex + 4) * (180f / (float)Math.PI);
                            lastYaw = BitConverter.ToSingle(buffer, payloadIndex + 8) * (180f / (float)Math.PI);

                            lastAttitudeUpdateTime = DateTime.Now;
                        }

                        idx += headerSize + len + 2; // +2 = CRC
                    }
                    else
                    {
                        idx++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Veri iþleme hatasý: " + ex.Message);
                lastLatitude = "GPS alýnamadý";
                lastLongitude = "GPS alýnamadý";
                lastYaw = null;
                lastPitch = null;
                lastRoll = null;
                lastAltitude = null;
                lastGpsTime = null;
            }
        }

        private void textBox10_TextChanged(object sender, EventArgs e) { }
        private void SendHeartbeat()
        {
            var heartbeat = new mavlink_heartbeat_t
            {
                type = (byte)MAV_TYPE.GCS,
                autopilot = (byte)MAV_AUTOPILOT.GENERIC,
                base_mode = 0,
                custom_mode = 0,
                system_status = (byte)MAV_STATE.ACTIVE,
                mavlink_version = 3
            };

            byte[] payload = StructToBytes(heartbeat);
            byte msgId = 0;
            byte[] packet = new byte[6 + payload.Length + 2];
            int i = 0;
            packet[i++] = 0xFE;
            packet[i++] = (byte)payload.Length;
            packet[i++] = seq++;
            packet[i++] = systemId;
            packet[i++] = componentId;
            packet[i++] = msgId;
            Array.Copy(payload, 0, packet, i, payload.Length);
            i += payload.Length;
            ushort crc = X25Crc(packet, 1, payload.Length + 5);
            crc = crc_accumulate(50, crc);
            packet[i++] = (byte)(crc & 0xFF);
            packet[i++] = (byte)((crc >> 8) & 0xFF);

            try
            {
                serialPort?.Write(packet, 0, packet.Length);
            }
            catch { }
        }

        private byte[] StructToBytes<T>(T msg)
        {
            int size = Marshal.SizeOf(msg);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(msg, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        private ushort X25Crc(byte[] buffer, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + count; i++)
                crc = crc_accumulate(buffer[i], crc);
            return crc;
        }

        private ushort crc_accumulate(byte b, ushort crc)
        {
            byte tmp = (byte)(b ^ (byte)(crc & 0xFF));
            tmp ^= (byte)(tmp << 4);
            return (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
        }

        private void textBox9_TextChanged(object sender, EventArgs e) { }
        private void textBox17_TextChanged(object sender, EventArgs e) { }
        private void textBox4_TextChanged(object sender, EventArgs e) { }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
        private void textBox3_TextChanged(object sender, EventArgs e) { }
        private void textBox14_TextChanged(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }

        private void textBox13_TextChanged(object sender, EventArgs e)
        {
        }

        private void webView21_Click(object sender, EventArgs e) { }
        private void panelHarita_Paint(object sender, PaintEventArgs e) { }

        private void button4_Click(object sender, EventArgs e)
        {
            if (!cameraUpdateTimer.Enabled)
            {
                cameraUpdateTimer.Start();
                MessageBox.Show("Kamera baþlatýldý.");
            }
            else
            {
                MessageBox.Show("Kamera zaten çalýþýyor.");
            }
        }

        private void pictureBox4_Click(object sender, EventArgs e) { }
        private void cameraUpdateTimer_Tick_1(object sender, EventArgs e) { }

        private void panel10_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel9_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            // ayarlar görseli 
        }
    }
}
