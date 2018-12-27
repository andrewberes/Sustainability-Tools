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

            PushButtonData curData = new PushButtonData("SustainabilityTools", "SustainabilityTools", curAssemblyName, curClassName);


            string imgIconLg = curAssemblyFolder + @"\Resources\icon_ST_large.bmp";
            string imgIconSm = curAssemblyFolder + @"\Resources\icon_ST_small.bmp";

            try
            {
                curData.Image = new BitmapImage(new Uri(imgIconSm));
                curData.LargeImage = new BitmapImage(new Uri(imgIconLg));
                
            }
            catch (Exception)
            {
                TaskDialog.Show("Error", "Cannot load icon image");
            }

            // add button to ribbon
            curPanel.AddItem(curData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
