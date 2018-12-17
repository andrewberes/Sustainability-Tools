/*
 * Created by SharpDevelop.
 * User: aberes
 * Date: 12/12/2018
 * Time: 10:01 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;

namespace fillSchedule
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("E67689AE-3701-47D3-A825-9C1B889C5505")]
	public partial class ThisDocument
	{
		private void Module_Startup(object sender, EventArgs e)
		{

		}

		private void Module_Shutdown(object sender, EventArgs e)
		{

		}

		#region Revit Macros generated code
		private void InternalStartup()
		{
			this.Startup += new System.EventHandler(Module_Startup);
			this.Shutdown += new System.EventHandler(Module_Shutdown);
		}
		#endregion
		public void fillSchedule()
		{
			// Get the current Document
			Document curDoc = this.Application.ActiveUIDocument.Document;
			
			//Get the current View
			View curView = this.Application.ActiveUIDocument.Document.ActiveView;
			
			// Get all rooms; Divide into point grid; Return points in curPointList
			RoomFilter filter = new RoomFilter();
			
			FilteredElementCollector roomCollector = new FilteredElementCollector(curDoc, curView.Id);
			roomCollector.WherePasses(filter);
			
			
			
			IEnumerable<Element> regOccupyRoomCollector = roomCollector.Where( a => a.LookupParameter("LEED Occupancy Type").AsString() == "REGULARLY OCCUPIED SPACES (CORE LEARNING)" ||
			                                                                        a.LookupParameter("LEED Occupancy Type").AsString() == "REGULARLY OCCUPIED SPACES (ANCILLARY LEARNING)" ||
			                                                                        a.LookupParameter("LEED Occupancy Type").AsString() == "OTHER REGULARLY OCCUPIED SPACES") ;
			
			// Get all filled regions in curView
			FilteredElementCollector fillCollector = new FilteredElementCollector(curDoc, curView.Id);
			
			fillCollector.OfClass(typeof(FilledRegion));
			
			TaskDialog.Show("test", fillCollector.Count().ToString() +" Filled Regions -> " + regOccupyRoomCollector.Count().ToString() + " Reg. Occupied Rooms of " + roomCollector.Count().ToString() + " total Rooms");
			
			
			string pathDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string filePath = pathDesktop + "\\AreasWithViews.csv";
			
			TaskDialog.Show("test", filePath);
			if (!File.Exists(filePath))
			{
			    File.Create(filePath).Close();
			}
			
			string delimter = ",";
			
			List<string[]> output = new List<string[]>();
			
			foreach (Room curRoom in regOccupyRoomCollector) 
			{
				string rmViewArea = "0";
				
				foreach (FilledRegion fR in fillCollector) 
				{
					Options opt = new Options();
					GeometryElement geomElem = fR.get_Geometry(opt);
					
					foreach (GeometryObject geomObj in geomElem)
					{
						Solid geomSolid = geomObj as Solid;
						
						if (null!= geomSolid) 
						{
							Face geomFace = geomSolid.Faces.get_Item(0);
							BoundingBoxUV bboxUV = geomFace.GetBoundingBox();
							UV centerUV = (bboxUV.Max + bboxUV.Min) /2;
							XYZ center = geomFace.Evaluate(centerUV);
							
							if (curRoom.IsPointInRoom(center) == true) 
							{
								rmViewArea = geomFace.Area.ToString();
							}
								
							
						}
					}
					
				}
				String rmName = curRoom.Name.ToString();
				String rmNumber = curRoom.Number.ToString();
				String rmArea = curRoom.Area.ToString();
				output.Add(new String[] {rmName, rmNumber, rmArea, rmViewArea});
				
				
			}
			
			
			
			
			int length = output.Count;

			using (System.IO.TextWriter writer = File.CreateText(filePath))
			{
			    for (int index = 0; index < length; index++)
			    {
			        writer.WriteLine(string.Join(delimter, output[index]));
			    }
			}
			
		}
		
	}
}