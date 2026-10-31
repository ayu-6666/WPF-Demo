using CPM.Recipe.Tool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPM.Recipe.Tool.Templates
{
    public static class UnloadRecipeTemplate
    {
        public static ObservableCollection<RecipeModel.Section> Template =
        [
            new()
          {
            Id = 1,
            Name = "Process Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 2,
            Default = "",
            Options = ["15:Process Unload", "-1:Repice End"],
            Steps = []
          },
            new()
          {
            Id = 2,
            Name = "Step Control",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 10,
            Default = "0",
            Options = [
              "0:Time",
              "1:Wafer In RB",
              "2:Z1",
              "3:Z2",
              "4:Z3",
              "5:Z4",
              "6:Z5",
              "7:Z6",
              "8:Z7",
              "9:Z8",
              "10:RR"
            ],
            Steps = []
          },
            new()
          {
            Id = 3,
            Name = "Zone Min Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "100",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 4,
            Name = "Zone Max Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "100",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 5,
            Name = "Max Time",
            Unit = "s",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "100",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 6,
            Name = "Zone 1 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 7,
            Name = "Zone 2 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 8,
            Name = "Zone 3 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 9,
            Name = "Zone 4 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 10,
            Name = "Zone 5 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 11,
            Name = "Zone 6 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 12,
            Name = "Zone 7 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 13,
            Name = "Zone 8 Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 14,
            Name = "TopRing Type",
            Unit = "",
            Type = "list",
            Min = 1,
            Max = 3,
            Default = "1",
            Options = ["1:Vent", "2:Vac", "3:Press"],
            Steps = []
          },
            new()
          {
            Id = 15,
            Name = "Zone 1 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 16,
            Name = "Zone 2 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 17,
            Name = "Zone 3 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 18,
            Name = "Zone 4 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 19,
            Name = "Zone 5 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 20,
            Name = "Zone 6 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 21,
            Name = "Zone 7 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 22,
            Name = "Zone 8 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 23,
            Name = "TopRing Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 24,
            Name = "Head Middle Wash",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "",
            Options = ["0:Close", "1:Open"],
            Steps = []
          },
            new()
          {
            Id = 25,
            Name = "Head External Wash",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "",
            Options = ["0:Close", "1:Open"],
            Steps = []
          },
            new()
          {
            Id = 26,
            Name = "Head Rotation",
            Unit = "r/min",
            Type = "float",
            Min = 0,
            Max = 200,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 27,
            Name = "Blow WF Select",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 2,
            Default = "0",
            Options = ["0:N2", "1:DIW", "2:All", "3:Disable"],
            Steps = []
          },
        ];
    }
}

