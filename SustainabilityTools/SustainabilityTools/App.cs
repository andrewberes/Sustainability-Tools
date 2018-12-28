#region Namespaces
using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.IO;
#endregion

namespace SustainabilityTools
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            RibbonPanel curPanel = a.CreateRibbonPanel("Sustainabilty Stuff");
            

            string curAssemblyName = Assembly.GetExecutingAssembly().Location;
            string curAssemblyFolder = Path.GetDirectoryName(curAssemblyName);
            string curClassName = "SustainabilityTools.Command";
            string twoClassName = "SustainabilityTools.Command2";

            PushButtonData pb1Data = new PushButtonData("ViewFilledRegions", "ViewFilledRegions", curAssemblyName, curClassName);
            pb1Data.ToolTip =  "Generate Filled Regions of the area within Room with access to an exterior view.";

            try
            {
                BitmapImage pb1Image = new BitmapImage(new Uri("pack://application:,,,/SustainabilityTools;component/Icons/viewFilledRegion.png"));

                pb1Data.LargeImage = pb1Image;

            }
            catch (Exception)
            {
                TaskDialog.Show("Error", "Cannot load icon image");
            }


            PushButtonData pb2Data = new PushButtonData("ExportViewSchedule", "ExportViewSchedule", curAssemblyName, twoClassName);

            pb1Data.ToolTip = "Export a schedule to myDesktop with all rooms visible in view, their areas and areas with an exterior view.";

            try
            {



                BitmapImage pb2Image = new BitmapImage(new Uri("pack://application:,,,/SustainabilityTools;component/Icons/viewExportSchedule.png"));

                pb2Data.LargeImage = pb2Image;

            }
            catch (Exception)
            {
                TaskDialog.Show("Error", "Cannot load icon image");
            }


            // add buttons to ribbon
            curPanel.AddItem(pb1Data);
            curPanel.AddItem(pb2Data);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
