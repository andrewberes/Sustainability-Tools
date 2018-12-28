#region Namespaces
using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using ClipperLib;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using System.IO;

#endregion

namespace SustainabilityTools
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;

            // Get the current Document
            Document curDoc = uiapp.ActiveUIDocument.Document;

            //Get the current View
            View curView = uidoc.Document.ActiveView;

            BoundingBoxXYZ curViewBoundary = curView.CropBox;


            FilteredElementCollector viewCollector = new FilteredElementCollector(curDoc);
            viewCollector.OfClass(typeof(View3D));
            List<View3D> view3dList = viewCollector.ToElements().Cast<View3D>().Where(x => x.IsTemplate == false).ToList();

            //TaskDialog.Show("test", view3dList.First().ViewName.ToString());


            //Build eyeLevel and eyeLevel sketchplane
            double eyeLevel = 5;
            XYZ eyeNormal = new XYZ(0, 0, 1);
            XYZ xAxis = new XYZ(1, 0, 0);
            XYZ yAxis = new XYZ(0, 1, 0);

            //Isovist variables
            int numRays = 600;
            double isoRadius = 300;
            double isoStartAngle = 0;
            double isoEndAngle = 360;

            //Convert to radians
            double radIsoStartAngle = (Math.PI / 180) * isoStartAngle;
            double radIsoEndAngle = (Math.PI / 180) * isoEndAngle;


            //Get all walls
            FilteredElementCollector wallCollector = new FilteredElementCollector(curDoc, curView.Id);
            wallCollector.OfCategory(BuiltInCategory.OST_Walls);


            //Get all stacked walls
            FilteredElementCollector stackedWallCollector = new FilteredElementCollector(curDoc, curView.Id);
            stackedWallCollector.OfCategory(BuiltInCategory.OST_StackedWalls);

            stackedWallCollector.UnionWith(wallCollector).WhereElementIsNotElementType();


            //TaskDialog.Show("Test", stackedWallCollector.Count().ToString() + " Walls");


            List<Face> wallFaces = new List<Face>();
            double totalArea = 0;
            List<Solid> wallSolids = new List<Solid>();

            foreach (Element curWall in stackedWallCollector)
            {
                Options opt = new Options();
                GeometryElement geomElem = curWall.get_Geometry(opt);


                foreach (GeometryObject geomObj in geomElem)
                {
                    Solid geomSolid = geomObj as Solid;
                    wallSolids.Add(geomSolid);


                    if (null != geomSolid)
                    {
                        foreach (Face geomFace in geomSolid.Faces)
                        {
                            totalArea += geomFace.Area;
                            wallFaces.Add(geomFace);
                        }
                    }
                }

            }

            //TaskDialog.Show("test", wallFaces.Count().ToString() + "  Faces");



            //Determine All Defaults for stuff later
            ElementId defaultView3d = curDoc.GetDefaultElementTypeId(ElementTypeGroup.ViewType3D);

            ElementId defaultTxtId = curDoc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            ElementId defaultFRId = curDoc.GetDefaultElementTypeId(ElementTypeGroup.FilledRegionType);




            //Create View Boundary filter for roomCollector

            Outline outline = new Outline(new XYZ(curViewBoundary.Min.X, curViewBoundary.Min.Y, -100), new XYZ(curViewBoundary.Max.X, curViewBoundary.Max.Y, 100));
            BoundingBoxIsInsideFilter bbFilter = new BoundingBoxIsInsideFilter(outline);

            //FilteredElementCollector roomList = roomCollector.WherePasses(bbFilter);


            // Get all rooms; Divide into point grid; Return points in curPointList
            RoomFilter filter = new RoomFilter();

            FilteredElementCollector roomCollector = new FilteredElementCollector(curDoc, curView.Id);
            roomCollector.WherePasses(filter).WherePasses(bbFilter);

            TaskDialog.Show("Magic View Region Drawing Machine", "Be sure to set the crop region around the rooms you would like to test, encompassing at least one exterior wall and all bounding elements." +
                            "Only rooms located fully within the bounds of the view range will be calculated.");
            TaskDialog.Show("Magic View Region Drawing Machine", roomCollector.Count().ToString() + " rooms fully visible in current view. The more rooms you run, the longer this will take!");

            Transaction t = new Transaction(curDoc, "Draw Some Lines");
            t.Start();

            View3D curView3d = View3D.CreateIsometric(curDoc, defaultView3d);

            IEnumerable<Element> extWalls = GetExteriorWalls(curDoc, curView);

            //TaskDialog.Show("test", extWalls.Count().ToString() +"  Exterior Walls");

            List<XYZ> somePoints = new List<XYZ>();

            foreach (Element wall in extWalls)
            {

                List<XYZ> pointsList = new List<XYZ>();


                pointsList = GetWallOpenings(wall, eyeLevel, curView3d);

                somePoints.AddRange(pointsList);


            }

            //TaskDialog.Show("test", somePoints.Count.ToString()+" points");


            List<List<Curve>> viewCones = new List<List<Curve>>();



            foreach (XYZ curPoint in somePoints)
            {
                Plane eyePlane = Plane.CreateByNormalAndOrigin(eyeNormal, curPoint);
                SketchPlane eyeSketchPlane = SketchPlane.Create(curDoc, eyePlane);

                //Arc arc = Arc.Create(curPoint, 2, 0, 360, xAxis, yAxis);
                //ModelCurve newArc = curDoc.Create.NewModelCurve(arc, eyeSketchPlane);



                List<Curve> outLineList = createIsovist(curPoint, numRays, isoRadius, radIsoStartAngle, radIsoEndAngle, curView, curDoc, eyeSketchPlane, wallFaces, defaultFRId);


                viewCones.Add(outLineList);
            }

            //Convert to polygons in int values
            Polygons polys = GetBoundaryLoops(viewCones);
            //TaskDialog.Show("test", polys.Count.ToString()+ " Polygons");


            //Calculate Intersection
            Polygons union = new Polygons();

            Clipper c = new Clipper();

            c.AddPath(polys[0], PolyType.ptSubject, true);

            for (int i = 1; i < polys.Count - 1; i++)
            {
                c.AddPath(polys[i], PolyType.ptClip, true);
            }

            c.Execute(ClipType.ctUnion, union, PolyFillType.pftPositive);

            /*
			
			TaskDialog.Show("test", union.Count.ToString()+ " Polygons");
			
			if (0 < union.Count) 
			{
				List<CurveLoop>regionLoops = new List<CurveLoop>();
				
				for (int p = 0; p < union.Count -1; p++) 
				{
					List<Curve> unionLineList = new List<Curve>();
					Polygon poly = union[p];
					
					for (int i = 1; i <= poly.Count-1; i++) 
					{
						unionLineList.Add(Line.CreateBound(GetXyzPoint(poly[i-1]), GetXyzPoint(poly[i])));
						
					}
					unionLineList.Add(Line.CreateBound(GetXyzPoint(poly[poly.Count-1]), GetXyzPoint(poly[0])));
					
					CurveLoop regionLoop = CurveLoop.Create(unionLineList);
					
					regionLoops.Add(regionLoop);
					
				}
				
				FilledRegion region = FilledRegion.Create(curDoc, defaultFRId, curView.Id, regionLoops);
				
				
			}
			*/


            foreach (Room curRoom in roomCollector)
            {
                SpatialElementBoundaryOptions bo = new SpatialElementBoundaryOptions();

                List<Curve> roomCurves = new List<Curve>();
                foreach (List<BoundarySegment> lstBs in curRoom.GetBoundarySegments(bo))
                {
                    foreach (BoundarySegment bs in lstBs)
                    {
                        roomCurves.Add(bs.GetCurve());
                    }
                }

                XYZ sketchPoint = new XYZ(0, 0, 0);

                Plane eyePlane = Plane.CreateByNormalAndOrigin(eyeNormal, sketchPoint);
                SketchPlane eyeSketchPlane = SketchPlane.Create(curDoc, eyePlane);


                List<List<Curve>> roomLoops = new List<List<Curve>>();

                roomLoops.Add(roomCurves);

                //Convert to polygon in int values
                Polygon roomPoly = GetBoundaryLoop(roomCurves);


                //Calculate Intersection
                Polygons intersection = new Polygons();

                Clipper c2 = new Clipper();

                c2.AddPath(roomPoly, PolyType.ptClip, true);
                c2.AddPaths(union, PolyType.ptSubject, true);

                c2.Execute(ClipType.ctIntersection, intersection, PolyFillType.pftPositive);

                //TaskDialog.Show("test", intersection.Count.ToString());


                if (0 < intersection.Count)
                {
                    List<CurveLoop> regionLoops = new List<CurveLoop>();
                    List<Curve> intersectionLineList = new List<Curve>();
                    Polygon poly = intersection[0];

                    IntPoint? p0 = null;
                    IntPoint? p = null;

                    foreach (IntPoint q in poly)
                    {
                        if (null == p0)
                        {
                            p0 = q;
                        }
                        if (null != p)
                        {
                            intersectionLineList.Add(Line.CreateBound(GetXyzPoint(p.Value), GetXyzPoint(q)));
                        }
                        p = q;
                    }

                    intersectionLineList.Add(Line.CreateBound(GetXyzPoint(poly[poly.Count - 1]), GetXyzPoint(poly[0])));

                    foreach (Curve cur in intersectionLineList)
                    {

                        //ModelCurve newArc = curDoc.Create.NewModelCurve(cur, eyeSketchPlane);
                    }


                    CurveLoop regionLoop = CurveLoop.Create(intersectionLineList);

                    regionLoops.Add(regionLoop);


                    //TaskDialog.Show("test", intersectionLineList.Count.ToString());




                    FilledRegion region = FilledRegion.Create(curDoc, defaultFRId, curView.Id, regionLoops);
                }



            }

            t.Commit();



            TaskDialog.Show("Magic View Region Drawing Machine", "Always double check the results, sometimes walls are missed. Tip: Curtain wall is prone to gliches and watch your door openings... Enjoy!");

            return Result.Succeeded;
        }


        public Polygons GetBoundaryLoops(List<List<Curve>> viewCones)
        {
            int n;
            Polygons polys = new Polygons();

            foreach (List<Curve> curveList in viewCones)
            {
                n = curveList.Count;
                Polygon poly = new Polygon(n);

                foreach (Curve curve in curveList)
                {
                    XYZ pt = curve.GetEndPoint(0);

                    poly.Add(GetIntPoint(pt));

                }

                polys.Add(poly);
            }

            //TaskDialog.Show("test", polys.Count.ToString());

            return polys;
        }

        public Polygon GetBoundaryLoop(List<Curve> curveList)
        {
            int n;

            n = curveList.Count;
            Polygon poly = new Polygon(n);

            foreach (Curve curve in curveList)
            {
                XYZ pt = curve.GetEndPoint(0);

                poly.Add(GetIntPoint(pt));

            }


            //TaskDialog.Show("test", poly.Count.ToString());

            return poly;
        }


        /// <summary>
        /// Conversion a given length value 
        /// from feet to millimetres.
        /// </summary>
        static long ConvertFeetToMillimetres(double d)
        {
            const double _eps = 1.0e-9;
            const double _feet_to_mm = 25.4 * 12;
            if (0 < d)
            {
                return _eps > d
                  ? 0
                  : (long)(_feet_to_mm * d + 0.5);

            }
            else
            {
                return _eps > -d
                  ? 0
                  : (long)(_feet_to_mm * d - 0.5);

            }
        }

        /// <summary>
        /// Conversion a given length value 
        /// from millimetres to feet.
        /// </summary>
        static double ConvertMillimetresToFeet(long d)
        {
            const double _feet_to_mm = 25.4 * 12;
            return d / _feet_to_mm;
        }

        /// <summary>
        /// Return a clipper integer point 
        /// from a Revit model space one.
        /// Do so by dropping the Z coordinate
        /// and converting from imperial feet 
        /// to millimetres.
        /// </summary>
        IntPoint GetIntPoint(XYZ p)
        {
            return new IntPoint(
              ConvertFeetToMillimetres(p.X),
              ConvertFeetToMillimetres(p.Y));
        }

        /// <summary>
        /// Return a Revit model space point 
        /// from a clipper integer one.
        /// Do so by adding a zero Z coordinate
        /// and converting from millimetres to
        /// imperial feet.
        /// </summary>
        XYZ GetXyzPoint(IntPoint p)
        {
            return new XYZ(
              ConvertMillimetresToFeet(p.X),
              ConvertMillimetresToFeet(p.Y),
              0.0);
        }




        public List<XYZ> GetWallOpenings(Element wall, double eyeLevel, View3D view)
        {
            List<XYZ> somePoints = new List<XYZ>();


            Curve c = (wall.Location as LocationCurve).Curve;
            XYZ wallOrigin = c.GetEndPoint(0);
            XYZ wallEndPoint = c.GetEndPoint(1);
            XYZ wallDirection = wallEndPoint - wallOrigin;
            double wallLength = wallDirection.GetLength();
            wallDirection = wallDirection.Normalize();
            UV offset = new UV(wallDirection.X, wallDirection.Y);
            double step_outside = offset.GetLength();

            XYZ rayStart = new XYZ(wallOrigin.X - offset.U, wallOrigin.Y - offset.V, eyeLevel);

            ReferenceIntersector intersector = new ReferenceIntersector(wall.Id, FindReferenceTarget.Face, view);

            IList<ReferenceWithContext> refs = intersector.Find(rayStart, wallDirection);

            List<XYZ> pointList = new List<XYZ>(refs.Select<ReferenceWithContext, XYZ>(r => r.GetReference().GlobalPoint));

            //TaskDialog.Show("title", pointList.Count.ToString());

            if (pointList.Count() >= 4)
            {
                pointList.RemoveAt(0);
                pointList.RemoveAt(0);
                somePoints.AddRange(pointList);
            }


            return somePoints;

        }

        public List<Curve> createIsovist(XYZ curPoint, int numRays, double radius, double startAngle, double endAngle, View curView, Document curDoc, SketchPlane eyeSketchPlane, List<Face> wallFaces, ElementId defaultFRId)
        {
            XYZ xAxis = new XYZ(1, 0, 0);
            XYZ yAxis = new XYZ(0, 1, 0);


            Arc arc = Arc.Create(curPoint, radius, startAngle, endAngle, xAxis, yAxis);
            //ModelCurve newArc = curDoc.Create.NewModelCurve(arc, eyeSketchPlane);

            int counter = 0;
            double meter = 0;
            double step = endAngle / numRays;

            List<Line> lineList = new List<Line>();

            for (int i = 0; i < numRays; i++)
            {

                XYZ arcParam = arc.Evaluate(meter, false);
                Line lineOut = Line.CreateBound(curPoint, arcParam);
                lineList.Add(lineOut);
                //ModelCurve newModelCurve = curDoc.Create.NewModelCurve(lineOut, eyeSketchPlane);

                meter = meter + step;

            }

            List<Line> interList = new List<Line>();
            List<Line> outerList = new List<Line>();
            List<double> lineLengths = new List<double>();


            foreach (Line curLine in lineList)
            {
                outerList.Clear();

                foreach (Face curFace in wallFaces)
                {
                    XYZ interPoint = intersectFaceCurve(curLine, curFace);
                    UV parms = new UV(.5, .5);
                    XYZ curFaceLocator = curFace.Evaluate(parms);

                    double curDistance = curPoint.DistanceTo(curFaceLocator);

                    if (curDistance <= radius - (radius * .1))
                    {
                        XYZ interPoints = intersectFaceCurve(curLine, curFace);

                        if (interPoints != null)
                        {

                            if (interPoints.DistanceTo(curPoint) >= .3)
                            {
                                Line interLine = Line.CreateBound(curPoint, interPoint);

                                outerList.Add(interLine);
                            }


                        }

                        counter++;
                    }

                }

                outerList.Add(curLine);


                lineLengths.Clear();

                foreach (Line outerLine in outerList)
                {
                    double lineLength = outerLine.Length;

                    lineLengths.Add(lineLength);
                }


                int indexMin = lineLengths.IndexOf(lineLengths.Min());
                interList.Add(outerList[indexMin]);



            }



            List<XYZ> endPoints = new List<XYZ>();
            foreach (Line line in interList)
            {
                if (line.Length != radius)
                {
                    endPoints.Add(line.GetEndPoint(1));
                }
            }



            List<Curve> outLineList = new List<Curve>();

            for (int i = 1; i <= endPoints.Count - 1; i++)
            {
                outLineList.Add(Line.CreateBound(endPoints[i - 1], endPoints[i]));

            }
            outLineList.Add(Line.CreateBound(endPoints[endPoints.Count - 1], endPoints[0]));

            CurveLoop regionLoop = CurveLoop.Create(outLineList);


            foreach (Line line in outLineList)
            {
                //ModelCurve newModelCurve = curDoc.Create.NewModelCurve(line, eyeSketchPlane);
            }

            List<CurveLoop> regionLoops = new List<CurveLoop>();
            regionLoops.Add(regionLoop);

            //FilledRegion region = FilledRegion.Create(curDoc, defaultFRId, curView.Id, regionLoops);



            return outLineList;



        }
        public XYZ intersectFaceCurve(Curve rayCurve, Face wallFace)
        {
            IntersectionResultArray intersectionR = new IntersectionResultArray();
            SetComparisonResult results;

            results = wallFace.Intersect(rayCurve, out intersectionR);

            XYZ intersectionResult = null;

            if (SetComparisonResult.Disjoint != results)
            {
                if (intersectionR != null)
                {
                    if (!intersectionR.IsEmpty)
                    {
                        intersectionResult = intersectionR.get_Item(0).XYZPoint;
                    }
                }
            }
            return intersectionResult;
        }

        public bool IsExterior(WallType wallType)
        {
            Parameter p = wallType.get_Parameter(
              BuiltInParameter.FUNCTION_PARAM);

            Debug.Assert(null != p, "expected wall type "
              + "to have wall function parameter");

            WallFunction f = (WallFunction)p.AsInteger();

            return WallFunction.Exterior == f;
        }
        public IEnumerable<Element> GetExteriorWalls(Document curDoc, View curView)
        {
            return new FilteredElementCollector(curDoc, curView.Id)
              .OfClass(typeof(Wall))
              .Cast<Wall>()
              .Where<Wall>(w =>
               IsExterior(w.WallType));
        }


    }
}
