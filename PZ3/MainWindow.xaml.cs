using PZ3.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace PZ3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Dictionary<long, SubstationEntity> Substations = new Dictionary<long, SubstationEntity>();
        Dictionary<long, LineEntity> Lines = new Dictionary<long, LineEntity>();
        Dictionary<long, NodeEntity> Nodes = new Dictionary<long, NodeEntity>();
        Dictionary<long, SwitchEntity> Switches = new Dictionary<long, SwitchEntity>();
       
        Dictionary<GeometryModel3D, long> AllEntities = new Dictionary<GeometryModel3D, long>();
       
        public ObservableCollection<string> UserOptions = new ObservableCollection<string>()
        {
            "Kompletan prikaz",
            "Prikazivanje objekata: konekcije od 0 do 3",
            "Prikazivanje objekata: konekcije od 3 do 5",
            "Prikazivanje objekata: konekcije vece od 5",
            "Sakrivanje objekata: konekcije od 0 do 3",
            "Sakrivanje objekata: konekcije od 3 do 5",
            "Sakrivanje objekata: konekcije vece od 5",

            "Prikazivanje vodova: otpornost od 0 do 1",
            "Prikazivanje vodova: otpornost od 1 do 2",
            "Prikazivanje vodova: otpornost veca od 2",
            "Sakrivanje vodova: otpornost od 0 do 1",
            "Sakrivanje vodova: otpornost od 1 do 2",
            "Sakrivanje vodova: otpornost veca od 2",

        };


        double minX = 45.2325;
        double maxX = 45.277031;
        double minY = 19.793909;
        double maxY = 19.894459;

        public double newX;
        public double newY;

        private Point startCoordinates = new Point();
        private Point diffOffset = new Point();
        private Point startRotation = new Point();

        private int CurrentZoom = 1;
        private int MaxZoom = 30;

        private double squareSize = 0.02;
        private double lineSize = 0.015;

        public Int32Collection IndiciesObjects = new Int32Collection()
        { 2, 3, 1, 2, 1, 0, 7, 1, 3, 7, 5, 1, 6, 5, 7, 6, 4, 5, 6, 2, 4, 2, 0, 4, 2, 7, 3, 2, 6, 7, 0, 1, 5, 0, 5, 4 };
       
        GeometryModel3D affectedEntity = null;
      
        long lineStartNodeID = -1;
        long lineEndNodeID = -1;
        int lineStartNodeType = -1;
        int lineEndNodeType = -1;

        List<string> NaziviMaterijala = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            izbor.ItemsSource = UserOptions;
            izbor.SelectedItem = UserOptions[0];
            LoadDataFromXML();
            CrtajCvorove();
            CrtajVodove();
        }

        public void CrtajCvorove()
        {
            double imageX;
            double imageY;
            int floorCounter = 0;

            foreach (SubstationEntity s in Substations.Values)
            {

                imageX = ScaleCoordinates(s.X, maxX, minX);
                imageY = ScaleCoordinates(s.Y, maxY, minY);

                GeometryModel3D substation = CreateModel(imageX, imageY, 0);

                while (CheckIfCoordinatesMatch((substation.Geometry as MeshGeometry3D).Positions))
                {
                    floorCounter++;
                    substation = CreateModel(imageX, imageY, 0, floorCounter * squareSize);
                }

                Mapa.Children.Add(substation);
                AllEntities.Add(substation, s.ID);
                floorCounter = 0;
            }


            foreach (NodeEntity n in Nodes.Values)
            {
                imageX = ScaleCoordinates(n.X, maxX, minX);
                imageY = ScaleCoordinates(n.Y, maxY, minY);

                GeometryModel3D node = CreateModel(imageX, imageY, 1);

                while (CheckIfCoordinatesMatch((node.Geometry as MeshGeometry3D).Positions))
                {
                    floorCounter++;
                    node = CreateModel(imageX, imageY, 1, floorCounter * squareSize);
                }

                Mapa.Children.Add(node);
                AllEntities.Add(node, n.ID);
                floorCounter = 0; //restart za sledeci node, a kad zavrsi foreach restartovan ce biti za sledeci Cvor-SWITCH;
            }

            foreach (SwitchEntity s in Switches.Values)
            {
                imageX = ScaleCoordinates(s.X, maxX, minX);
                imageY = ScaleCoordinates(s.Y, maxY, minY);

                GeometryModel3D sw = CreateModel(imageX, imageY, 2);

                while (CheckIfCoordinatesMatch((sw.Geometry as MeshGeometry3D).Positions))
                {
                    floorCounter++;
                    sw = CreateModel(imageX, imageY, 2, floorCounter * squareSize);
                }

                Mapa.Children.Add(sw);
                AllEntities.Add(sw, s.ID);
                floorCounter = 0; //restart za sledeci switch, a kad zavrsi foreach ovaj brojac se nece koristiti :)

            }
        }

        private bool CheckIfCoordinatesMatch(Point3DCollection elementCoord)
        {
            foreach (GeometryModel3D item in AllEntities.Keys)
            {
                Point3DCollection coordinates = (item.Geometry as MeshGeometry3D).Positions;

                if (coordinates[0].X == elementCoord[0].X && coordinates[0].Y == elementCoord[0].Y && coordinates[0].Z == elementCoord[0].Z)
                    return true;

               if (coordinates[0].Z == elementCoord[0].Z)
                {
                    Rect r1 = new Rect(new Point(elementCoord[0].X, elementCoord[0].Y), new Point(elementCoord[3].X, elementCoord[3].Y));
                    Rect r2 = new Rect(new Point(coordinates[0].X, coordinates[0].Y), new Point(coordinates[3].X, coordinates[3].Y));
                    Rect r3 = Rect.Intersect(r1, r2); //vraca empty ako nema presecanja

                    if (r3 != Rect.Empty)
                        return true;
                }
            }
            return false;
        }

        private GeometryModel3D CreateModel(double imageX, double imageY, int tip, double imageZ = 0)
        {
            //tip - boja za odredjeni cvor
            // 0 substation - black
            // 1 node - green
            // 2 switch - blue

            Point3DCollection positions = new Point3DCollection();

            positions.Add(new Point3D(imageY, imageX, imageZ));
            positions.Add(new Point3D(imageY + squareSize, imageX, imageZ));
            positions.Add(new Point3D(imageY, imageX + squareSize, imageZ));
            positions.Add(new Point3D(imageY + squareSize, imageX + squareSize, imageZ));
            positions.Add(new Point3D(imageY, imageX, imageZ + squareSize));
            positions.Add(new Point3D(imageY + squareSize, imageX, imageZ + squareSize));
            positions.Add(new Point3D(imageY, imageX + squareSize, imageZ + squareSize));
            positions.Add(new Point3D(imageY + squareSize, imageX + squareSize, imageZ + squareSize));

            GeometryModel3D node = new GeometryModel3D();

            if (tip == 0)
                node.Material = new DiffuseMaterial(Brushes.Black);
            else if (tip == 1)
                node.Material = new DiffuseMaterial(Brushes.Green);
            else if (tip == 2)
                node.Material = new DiffuseMaterial(Brushes.Blue);

            node.Geometry = new MeshGeometry3D()
            {
                Positions = positions,
                TriangleIndices = IndiciesObjects,
            };

            return node;
        }

        private void CrtajVodove()
        {
            double imageX, imageY;
            double susedImageX, susedImageY;

            foreach (LineEntity line in Lines.Values)
            {
                for (int i = 0; i < line.Vertices.Count - 1; i++)
                {
                    Point3DCollection positions = new Point3DCollection();
                    GeometryModel3D vod = new GeometryModel3D();

                    if (line.Material == "Steel")
                        vod.Material = new DiffuseMaterial(Brushes.Red);

                    else if (line.Material == "Acsr")
                        vod.Material = new DiffuseMaterial(Brushes.Orange);

                    else if (line.Material == "Copper")
                        vod.Material = new DiffuseMaterial(Brushes.Yellow);

                    imageX = ScaleCoordinates(line.Vertices[i].X, maxX, minX);
                    imageY = ScaleCoordinates(line.Vertices[i].Y, maxY, minY);

                    susedImageX = ScaleCoordinates(line.Vertices[i + 1].X, maxX, minX);
                    susedImageY = ScaleCoordinates(line.Vertices[i + 1].Y, maxY, minY);

                    double differenceX = imageX - susedImageX;
                    double differenceY = imageY - susedImageY;
                    
                    if (differenceX > 0 && differenceY > 0 || differenceX > 0)
                    {
                        positions.Add(new Point3D(susedImageY, susedImageX, 0));
                        positions.Add(new Point3D(susedImageY + lineSize, susedImageX, 0));
                        positions.Add(new Point3D(imageY, imageX, 0));
                        positions.Add(new Point3D(imageY + lineSize, imageX, 0));
                        positions.Add(new Point3D(susedImageY, susedImageX, lineSize));
                        positions.Add(new Point3D(susedImageY + lineSize, susedImageX, lineSize));
                        positions.Add(new Point3D(imageY, imageX, lineSize));
                        positions.Add(new Point3D(imageY + lineSize, imageX, lineSize));
                    }
                    else
                    {
                        positions.Add(new Point3D(imageY, imageX, 0));
                        positions.Add(new Point3D(imageY + lineSize, imageX, 0));
                        positions.Add(new Point3D(susedImageY, susedImageX, 0));
                        positions.Add(new Point3D(susedImageY + lineSize, susedImageX, 0));
                        positions.Add(new Point3D(imageY, imageX, lineSize));
                        positions.Add(new Point3D(imageY + lineSize, imageX, lineSize));
                        positions.Add(new Point3D(susedImageY, susedImageX, lineSize));
                        positions.Add(new Point3D(susedImageY + lineSize, susedImageX, lineSize));
                    }

                    vod.Geometry = new MeshGeometry3D()
                    {
                        Positions = positions,
                        TriangleIndices = IndiciesObjects,
                    };

                    Mapa.Children.Add(vod);

                    AllEntities.Add(vod, line.ID);
                }
            }
        }

        private double ScaleCoordinates(double X_or_Y, double max, double min)
        {
            double retValue = 0;

            retValue = (X_or_Y - min) / ((max - min) * (0.5));

            return retValue;
        }

        public void LoadDataFromXML()
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load("Geographic.xml");

            XmlNodeList xmlNodeList;

            xmlNodeList = xmlDocument.DocumentElement.SelectNodes("/NetworkModel/Substations/SubstationEntity");

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                SubstationEntity s = new SubstationEntity();
                s.ID = long.Parse(xmlNode.SelectSingleNode("Id").InnerText);
                s.Name = xmlNode.SelectSingleNode("Name").InnerText;
                s.X = double.Parse(xmlNode.SelectSingleNode("X").InnerText);
                s.Y = double.Parse(xmlNode.SelectSingleNode("Y").InnerText);

                ToLatLon(s.X, s.Y, 34, out newX, out newY);
                s.X = newX;
                s.Y = newY;
                
                if (s.X < minX || s.X > maxX || s.Y < minY || s.Y > maxY)
                    continue;

                Substations.Add(s.ID, s);
            }

            xmlNodeList = xmlDocument.DocumentElement.SelectNodes("/NetworkModel/Nodes/NodeEntity");

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                NodeEntity n = new NodeEntity();
                n.ID = long.Parse(xmlNode.SelectSingleNode("Id").InnerText);
                n.Name = xmlNode.SelectSingleNode("Name").InnerText;
                n.X = double.Parse(xmlNode.SelectSingleNode("X").InnerText);
                n.Y = double.Parse(xmlNode.SelectSingleNode("Y").InnerText);

                ToLatLon(n.X, n.Y, 34, out newX, out newY);
                n.X = newX;
                n.Y = newY;
                if (n.X < minX || n.X > maxX || n.Y < minY || n.Y > maxY)
                    continue;

                Nodes.Add(n.ID, n);
            }

            xmlNodeList = xmlDocument.DocumentElement.SelectNodes("/NetworkModel/Switches/SwitchEntity");

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                SwitchEntity s = new SwitchEntity();
                s.ID = long.Parse(xmlNode.SelectSingleNode("Id").InnerText);
                s.Name = xmlNode.SelectSingleNode("Name").InnerText;
                s.X = double.Parse(xmlNode.SelectSingleNode("X").InnerText);
                s.Y = double.Parse(xmlNode.SelectSingleNode("Y").InnerText);
                s.Status = xmlNode.SelectSingleNode("Status").InnerText;

                ToLatLon(s.X, s.Y, 34, out newX, out newY);
                s.X = newX;
                s.Y = newY;
                if (s.X < minX || s.X > maxX || s.Y < minY || s.Y > maxY)
                    continue;

                Switches.Add(s.ID, s);
            }

            xmlNodeList = xmlDocument.DocumentElement.SelectNodes("/NetworkModel/Lines/LineEntity");

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                LineEntity l = new LineEntity();
                l.ID = long.Parse(xmlNode.SelectSingleNode("Id").InnerText);
                l.Name = xmlNode.SelectSingleNode("Name").InnerText;
                l.StartNodeID = long.Parse(xmlNode.SelectSingleNode("FirstEnd").InnerText);
                l.EndNodeID = long.Parse(xmlNode.SelectSingleNode("SecondEnd").InnerText);
                l.Resistance = double.Parse(xmlNode.SelectSingleNode("R").InnerText);
                l.Material = xmlNode.SelectSingleNode("ConductorMaterial").InnerText;
                
                if (!ShouldLineEntityBeOnMap(l))
                    continue;

                l.Vertices = new List<PointEntity>();
                int verticesCounter = 0;
                foreach (XmlNode item in xmlNode.ChildNodes[9].ChildNodes)
                {
                    verticesCounter++;

                    PointEntity p = new PointEntity();
                    p.X = double.Parse(item.SelectSingleNode("X").InnerText);
                    p.Y = double.Parse(item.SelectSingleNode("Y").InnerText);

                    ToLatLon(p.X, p.Y, 34, out newX, out newY);
                    p.X = newX;
                    p.Y = newY;

                    if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY)
                        continue;

                    l.Vertices.Add(p);
                }

                if (l.Vertices.Count == verticesCounter)
                {
                    double cvorX, cvorY;

                    RetrieveCoordinatesForSpecificNode(l.StartNodeID, out cvorX, out cvorY);
                    PointEntity start = new PointEntity() { X = cvorX, Y = cvorY };

                    RetrieveCoordinatesForSpecificNode(l.EndNodeID, out cvorX, out cvorY);
                    PointEntity end = new PointEntity() { X = cvorX, Y = cvorY };

                    l.Vertices.Insert(0, start);
                    l.Vertices.Add(end);
                    Lines.Add(l.ID, l);
                }
            }

        }

        private void RetrieveCoordinatesForSpecificNode(long idCvora, out double x, out double y)
        {
            if (Substations.ContainsKey(idCvora))
            {
                x = Substations[idCvora].X;
                y = Substations[idCvora].Y;
                Substations[idCvora].ConnectionCounter++;
            }
            else if (Switches.ContainsKey(idCvora))
            {
                x = Switches[idCvora].X;
                y = Switches[idCvora].Y;
                Switches[idCvora].ConnectionCounter++;
            }
            else 
            {
                x = Nodes[idCvora].X;
                y = Nodes[idCvora].Y;
                Nodes[idCvora].ConnectionCounter++;
            }
        }

        private bool ShouldLineEntityBeOnMap(LineEntity l)
        {
            if (Substations.ContainsKey(l.StartNodeID) || Nodes.ContainsKey(l.StartNodeID) || Switches.ContainsKey(l.StartNodeID))
            {
                if (Substations.ContainsKey(l.EndNodeID) || Nodes.ContainsKey(l.EndNodeID) || Switches.ContainsKey(l.EndNodeID))
                    return true;
            }
            return false;
        }

        //From UTM to Latitude and longitude in decimal
        public static void ToLatLon(double utmX, double utmY, int zoneUTM, out double latitude, out double longitude)
        {
            bool isNorthHemisphere = true;

            var diflat = -0.00066286966871111111111111111111111111;
            var diflon = -0.0003868060578;

            var zone = zoneUTM;
            var c_sa = 6378137.000000;
            var c_sb = 6356752.314245;
            var e2 = Math.Pow((Math.Pow(c_sa, 2) - Math.Pow(c_sb, 2)), 0.5) / c_sb;
            var e2cuadrada = Math.Pow(e2, 2);
            var c = Math.Pow(c_sa, 2) / c_sb;
            var x = utmX - 500000;
            var y = isNorthHemisphere ? utmY : utmY - 10000000;

            var s = ((zone * 6.0) - 183.0);
            var lat = y / (c_sa * 0.9996);
            var v = (c / Math.Pow(1 + (e2cuadrada * Math.Pow(Math.Cos(lat), 2)), 0.5)) * 0.9996;
            var a = x / v;
            var a1 = Math.Sin(2 * lat);
            var a2 = a1 * Math.Pow((Math.Cos(lat)), 2);
            var j2 = lat + (a1 / 2.0);
            var j4 = ((3 * j2) + a2) / 4.0;
            var j6 = ((5 * j4) + Math.Pow(a2 * (Math.Cos(lat)), 2)) / 3.0;
            var alfa = (3.0 / 4.0) * e2cuadrada;
            var beta = (5.0 / 3.0) * Math.Pow(alfa, 2);
            var gama = (35.0 / 27.0) * Math.Pow(alfa, 3);
            var bm = 0.9996 * c * (lat - alfa * j2 + beta * j4 - gama * j6);
            var b = (y - bm) / v;
            var epsi = ((e2cuadrada * Math.Pow(a, 2)) / 2.0) * Math.Pow((Math.Cos(lat)), 2);
            var eps = a * (1 - (epsi / 3.0));
            var nab = (b * (1 - epsi)) + lat;
            var senoheps = (Math.Exp(eps) - Math.Exp(-eps)) / 2.0;
            var delt = Math.Atan(senoheps / (Math.Cos(nab)));
            var tao = Math.Atan(Math.Cos(delt) * Math.Tan(nab));

            longitude = ((delt * (180.0 / Math.PI)) + s) + diflon;
            latitude = ((lat + (1 + e2cuadrada * Math.Pow(Math.Cos(lat), 2) - (3.0 / 2.0) * e2cuadrada * Math.Sin(lat) * Math.Cos(lat) * (tao - lat)) * (tao - lat)) * (180.0 / Math.PI)) + diflat;
        }

        public void Scena_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scena.ReleaseMouseCapture();
        }

        private void Scena_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                startRotation = e.GetPosition(this);
            }
        }

        public void Scena_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            scena.CaptureMouse();
            startCoordinates = e.GetPosition(this);
            diffOffset.X = transliranje.OffsetX;
            diffOffset.Y = transliranje.OffsetY;
        }

        public void Scena_MouseMove(object sender, MouseEventArgs e)
        {

            if (scena.IsMouseCaptured)
            {
                Point endCoordinates = e.GetPosition(this);
                double offsetX = endCoordinates.X - startCoordinates.X;
                double offsetY = endCoordinates.Y - startCoordinates.Y;
                double w = this.Width;
                double h = this.Height;
                double translateX = (offsetX * 100) / w;
                double translateY = -(offsetY * 100) / h;
                transliranje.OffsetX = diffOffset.X + (translateX / (100 * skaliranje.ScaleX));
                transliranje.OffsetY = diffOffset.Y + (translateY / (100 * skaliranje.ScaleX));
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Point end = e.GetPosition(this);
                double offsetX = end.X - startRotation.X;
                double offsetY = end.Y - startRotation.Y;

             
                if ((AngleRotationX.Angle + (offsetY / 2) < 87 && AngleRotationX.Angle + (offsetY / 2) > -71))
                    AngleRotationX.Angle += offsetY/2;

                if ((AngleRotationY.Angle + (offsetX / 2)  < 82 && AngleRotationY.Angle + (offsetX / 2) > -71))
                    AngleRotationY.Angle += (offsetX / 2);

                startRotation = end;
            }
            else if (e.MiddleButton == MouseButtonState.Released)
             
                startRotation = new Point();
        }

        public void Scena_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0 && CurrentZoom < MaxZoom)
            {
                skaliranje.ScaleX += 0.1;
                skaliranje.ScaleY += 0.1;
                CurrentZoom++;
            }
            else if (e.Delta < 0 && CurrentZoom > -MaxZoom)
            {
                skaliranje.ScaleX -= 0.1;
                skaliranje.ScaleY -= 0.1;
                CurrentZoom--;
            }
        }

        public void ResetColorsForNodes()
        {
            //tip - boja za odredjeni cvor
            // 0 substation - black
            // 1 node - green
            // 2 switch - blue

            if (lineStartNodeID != -1 && lineEndNodeID != -1)
            {
                if (lineStartNodeType == 0)
                    AllEntities.FirstOrDefault(x => x.Value == lineStartNodeID).Key.Material = new DiffuseMaterial(Brushes.Black);
                else if (lineStartNodeType == 1)
                    AllEntities.FirstOrDefault(x => x.Value == lineStartNodeID).Key.Material = new DiffuseMaterial(Brushes.Green); 
                else if (lineStartNodeType == 2)
                    AllEntities.FirstOrDefault(x => x.Value == lineStartNodeID).Key.Material = new DiffuseMaterial(Brushes.Blue);

                if (lineEndNodeType == 0)
                    AllEntities.FirstOrDefault(x => x.Value == lineEndNodeID).Key.Material = new DiffuseMaterial(Brushes.Black);
                else if (lineEndNodeType == 1)
                    AllEntities.FirstOrDefault(x => x.Value == lineEndNodeID).Key.Material = new DiffuseMaterial(Brushes.Green); 
                else if (lineEndNodeType == 2)
                    AllEntities.FirstOrDefault(x => x.Value == lineEndNodeID).Key.Material = new DiffuseMaterial(Brushes.Blue); 
            }

        }

        public void Scena_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point mouseCoordinates = e.GetPosition(this);
            PointHitTestParameters pointparams = new PointHitTestParameters(mouseCoordinates);

            affectedEntity = null;
            VisualTreeHelper.HitTest(this, null, HTResult, pointparams);
        }

        private HitTestResultBehavior HTResult(HitTestResult rawresult)
        {
            RayHitTestResult rayResult = rawresult as RayHitTestResult;

            if (rayResult != null)
            {
                ResetColorsForNodes();

                lineStartNodeID = -1;
                lineEndNodeID = -1;

                if (AllEntities.ContainsKey(rayResult.ModelHit as GeometryModel3D))
                {
                    affectedEntity = (GeometryModel3D)rayResult.ModelHit;
                }
                if (affectedEntity == null) return HitTestResultBehavior.Stop;

                long Id = AllEntities[affectedEntity]; 
                ToolTip tt = new ToolTip();
                tt.StaysOpen = false;


                if (Substations.ContainsKey(Id))
                {
                    tt.Content = "Substation\n" + "ID: " + Id + "\nName: " + Substations[Id].Name + "\nConnections: "
                                + Substations[Id].ConnectionCounter;
                    tt.IsOpen = true;
                }

                if (Nodes.ContainsKey(Id))
                {
                    tt.Content = "Node\n" + "ID: " + Id + "\nName: " + Nodes[Id].Name + "\nConnections: "
                                + Nodes[Id].ConnectionCounter; ;
                    tt.IsOpen = true;
                }

                if (Switches.ContainsKey(Id))
                {
                    tt.Content = "Switch\n" + "ID: " + Id + "\nName: " + Switches[Id].Name + "\nConnections: "
                                + Switches[Id].ConnectionCounter + "\nStatus: " + Switches[Id].Status;
                    tt.IsOpen = true;
                }

                if (Lines.ContainsKey(Id))
                {
                    LineEntity line = Lines[Id];
                    tt.Content = "Line\n" + "ID: " + Id + "\nName: " + line.Name + "\nStartNode: " + line.StartNodeID +
                                 "\nEndNode: " + line.EndNodeID  + "\nResistance: " + line.Resistance;
                    tt.IsOpen = true;

                    AllEntities.FirstOrDefault(x => x.Value == line.StartNodeID).Key.Material = new DiffuseMaterial(Brushes.Chocolate);
                    AllEntities.FirstOrDefault(x => x.Value == line.EndNodeID).Key.Material = new DiffuseMaterial(Brushes.Chocolate);

                    lineStartNodeType = GetTypeForEntity(line.StartNodeID);
                    lineEndNodeType = GetTypeForEntity(line.EndNodeID);

                    lineStartNodeID = line.StartNodeID;
                    lineEndNodeID = line.EndNodeID;
                }

            }
            return HitTestResultBehavior.Stop;
        }

        private int GetTypeForEntity(long id)
        {
            //tip - boja za odredjeni cvor
            // 0 substation - black
            // 1 node - green
            // 2 switch - blue

            if (Substations.ContainsKey(id))
                return 0;
            else if (Nodes.ContainsKey(id))
                return 1;
            else if (Switches.ContainsKey(id))
                return 2;
            else
                return -1;
        }



        private void Izbor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CompleteView();
            switch (izbor.SelectedItem)
            {
                case "Prikazivanje objekata: konekcije od 0 do 3":
                    ShowHideKnots(true, 0);
                    break;
                case "Prikazivanje objekata: konekcije od 3 do 5":
                    ShowHideKnots(true, 1);
                    break;
                case "Prikazivanje objekata: konekcije vece od 5":
                    ShowHideKnots(true, 2);
                    break;
                case "Sakrivanje objekata: konekcije od 0 do 3":
                    ShowHideKnots(false, 0);
                    break;
                case "Sakrivanje objekata: konekcije od 3 do 5":
                    ShowHideKnots(false, 1);
                    break;
                case "Sakrivanje objekata: konekcije vece od 5":
                    ShowHideKnots(false, 2);
                    break;
                case "Prikazivanje vodova: otpornost od 0 do 1":
                    ShowHideLines(true, 0);
                    break;
                case "Prikazivanje vodova: otpornost od 1 do 2":
                    ShowHideLines(true, 1);
                    break;
                case "Prikazivanje vodova: otpornost veca od 2":
                    ShowHideLines(true, 2);
                    break;
                case "Sakrivanje vodova: otpornost od 0 do 1":
                    ShowHideLines(false, 0);
                    break;
                case "Sakrivanje vodova: otpornost od 1 do 2":
                    ShowHideLines(false, 1);
                    break;
                case "Sakrivanje vodova: otpornost veca od 2":
                    ShowHideLines(false, 2);
                    break;
                default: break;
            }
        }

        private void CompleteView()
        {
            foreach (KeyValuePair<GeometryModel3D, long> entitetSaMape in AllEntities)
            {
                if (!Mapa.Children.Contains(entitetSaMape.Key))
                {
                    Mapa.Children.Add(entitetSaMape.Key);
                }
            }
        }

        private void ShowHideKnots(bool prikaz, int opcija)
        {
           

            foreach (KeyValuePair<GeometryModel3D, long> entitetSaMape in AllEntities)
            {
                if (Substations.ContainsKey(entitetSaMape.Value))
                {
                    if (prikaz)
                    {
                        if (opcija == 0)
                        {
                            if (Substations[entitetSaMape.Value].ConnectionCounter >= 3)
                                Mapa.Children.Remove(entitetSaMape.Key);

                        }
                        else if (opcija == 1)
                        {
                            if (Substations[entitetSaMape.Value].ConnectionCounter < 3 || Substations[entitetSaMape.Value].ConnectionCounter > 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Substations[entitetSaMape.Value].ConnectionCounter <= 5)
                                Mapa.Children.Remove(entitetSaMape.Key);

                        }

                    }
                    else
                    {
                        if (opcija == 0)
                        {
                            if (Substations[entitetSaMape.Value].ConnectionCounter < 3)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 1)
                        {
                            if (Substations[entitetSaMape.Value].ConnectionCounter >= 3 && Substations[entitetSaMape.Value].ConnectionCounter <= 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Substations[entitetSaMape.Value].ConnectionCounter > 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }
                }
                else if (Nodes.ContainsKey(entitetSaMape.Value))
                {
                    if (prikaz)
                    {
                        if (opcija == 0)
                        {
                            if (Nodes[entitetSaMape.Value].ConnectionCounter >= 3)
                                Mapa.Children.Remove(entitetSaMape.Key);

                        }
                        else if (opcija == 1)
                        {
                            if (Nodes[entitetSaMape.Value].ConnectionCounter < 3 || Nodes[entitetSaMape.Value].ConnectionCounter > 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Nodes[entitetSaMape.Value].ConnectionCounter <= 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }
                    else
                    {
                        if (opcija == 0)
                        {
                            if (Nodes[entitetSaMape.Value].ConnectionCounter < 3)
                                Mapa.Children.Remove(entitetSaMape.Key);

                        }
                        else if (opcija == 1)
                        {
                            if (Nodes[entitetSaMape.Value].ConnectionCounter >= 3 && Nodes[entitetSaMape.Value].ConnectionCounter <= 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Nodes[entitetSaMape.Value].ConnectionCounter > 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }

                }
                else if (Switches.ContainsKey(entitetSaMape.Value))
                {
                    if (prikaz)
                    {
                        if (opcija == 0)
                        {
                            if (Switches[entitetSaMape.Value].ConnectionCounter >= 3)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 1)
                        {
                            if (Switches[entitetSaMape.Value].ConnectionCounter < 3 || Switches[entitetSaMape.Value].ConnectionCounter > 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Switches[entitetSaMape.Value].ConnectionCounter <= 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }
                    else
                    {
                       
                        if (opcija == 0)
                        {
                            if (Switches[entitetSaMape.Value].ConnectionCounter <= 3)
                                Mapa.Children.Remove(entitetSaMape.Key);

                        }
                        else if (opcija == 1)
                        {
                            if (Switches[entitetSaMape.Value].ConnectionCounter >= 3 && Switches[entitetSaMape.Value].ConnectionCounter <= 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Switches[entitetSaMape.Value].ConnectionCounter > 5)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }
                }
            }
        }

        private void ShowHideLines(bool prikaz, int opcija)
        {
           

            foreach (KeyValuePair<GeometryModel3D, long> entitetSaMape in AllEntities)
            {
                if (Lines.ContainsKey(entitetSaMape.Value))
                {
                    if (prikaz)
                    {
                        if (opcija == 0)
                        {
                            if (Lines[entitetSaMape.Value].Resistance >= 1)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 1)
                        {
                            if (Lines[entitetSaMape.Value].Resistance < 1 || Lines[entitetSaMape.Value].Resistance > 2)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Lines[entitetSaMape.Value].Resistance <= 2)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }
                    else
                    {
                      
                        if (opcija == 0)
                        {
                            if (Lines[entitetSaMape.Value].Resistance < 1)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 1)
                        {
                            if (Lines[entitetSaMape.Value].Resistance >= 1 && Lines[entitetSaMape.Value].Resistance <= 2)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                        else if (opcija == 2)
                        {
                            if (Lines[entitetSaMape.Value].Resistance > 2)
                                Mapa.Children.Remove(entitetSaMape.Key);
                        }
                    }
                }
            }
        }

        private void AktivanDeoMreze(object sender, RoutedEventArgs e)
        {
            CompleteView();

            if (aktivanDeo.IsChecked == true)
            {
                foreach (KeyValuePair<GeometryModel3D, long> entitetSaMape in AllEntities)
                {
                    if (Lines.ContainsKey(entitetSaMape.Value))
                    {
                        if (Switches.ContainsKey(Lines[entitetSaMape.Value].StartNodeID))
                            if (Switches[Lines[entitetSaMape.Value].StartNodeID].Status.ToLower().Equals("open"))
                                Mapa.Children.Remove(entitetSaMape.Key);
                    }
                }

            }
        }



    }
}
