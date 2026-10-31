using CPM.Recipe.Tool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPM.Recipe.Tool.Templates
{
    public static class PolishRecipeTemplate
    {
        public static ObservableCollection<RecipeModel.Section> Template =
        [
            new()
          {
            Id = 1,
            Name = "Process Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = ["1:Process Polish", "2:Main Polish", "-1:Recipe End"],
            Steps = []
          },
            new()
          {
            Id = 2,
            Name = "Process Time",
            Unit = "s",
            Type = "float",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 3,
            Name = "Jump Step No.",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = 
            [
              "1:Step1",
              "2:Step2",
              "3:Step3",
              "4:Step4",
              "5:Step5",
              "6:Step6",
              "7:Step7",
              "8:Step8",
              "9:Step9",
              "10:Step10",
              "11:Step11",
              "12:Step12",
              "13:Step13",
              "14:Step14",
              "15:Step15",
              "16:Step16",
              "17:Step17",
              "18:Step18",
              "19:Step19",
              "20:Step20",
              "21:Step21",
              "22:Step22",
              "23:Step23",
              "24:Step24",
              "25:Step25",
              "26:Step End",
              "-1:Not Set"
            ],
            Steps = []
          },
            new()
          {
            Id = 4,
            Name = "Platen Rotation",
            Unit = "r/min",
            Type = "float",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 5,
            Name = "Head Rotation",
            Unit = "r/min",
            Type = "float",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 6,
            Name = "Head Sweep Recipe",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:None",
              "1:Sweep Recipe1",
              "2:Sweep Recipe2",
              "3:Sweep Recipe3",
              "4:Sweep Recipe4",
              "5:Sweep Recipe5"
            ],
            Steps = []
          },
            new()
          {
            Id = 7,
            Name = "Zone 1 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 8,
            Name = "Zone 2 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 9,
            Name = "Zone 3 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 10,
            Name = "Zone 4 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 11,
            Name = "Zone 5 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 12,
            Name = "Zone 6 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 13,
            Name = "Zone 7 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 14,
            Name = "Zone 8 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 15,
            Name = "Top Ring Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "1:Vent",
              "2:Vac",
              "3:Press"
            ],
            Steps = []
          },
            new()
          {
            Id = 16,
            Name = "Zone 1 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 17,
            Name = "Zone 2 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 18,
            Name = "Zone 3 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 19,
            Name = "Zone 4 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 20,
            Name = "Zone 5 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 21,
            Name = "Zone 6 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 22,
            Name = "Zone 7 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 23,
            Name = "Zone 8 Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 24,
            Name = "Zone RR Pressure",
            Unit = "KPa",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 25,
            Name = "Condition Recipe",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:None",
              "1:Condition Recipe1",
              "2:Condition Recipe2",
              "3:Condition Recipe3",
              "4:Condition Recipe4",
              "5:Condition Recipe5"
            ],
            Steps = []
          },
            new()
          {
            Id = 26,
            Name = "Plant Wash",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:Close",
              "1:Open"
            ],
            Steps = []
          },
            new()
          {
            Id = 27,
            Name = "Slurry Line 1 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:Slurry",
              "1:DIW"
            ],
            Steps = []
          },
            new()
          {
            Id = 28,
            Name = "Slurry Line 2 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:Slurry",
              "1:DIW"
            ],
            Steps = []
          },
             new()
          {
            Id = 29,
            Name = "Slurry Line 3 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:Slurry",
              "1:DIW"
            ],
            Steps = []
          },
             new()
          {
            Id = 30,
            Name = "Slurry Line 1 Flow",
            Unit = "mL/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 31,
            Name = "Slurry Line 2 Flow",
            Unit = "mL/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 32,
            Name = "Slurry Line 3 Flow",
            Unit = "mL/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 33,
            Name = "DIW",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "0:Close",
              "1:Open"
            ],
            Steps = []
          },
            new()
          {
            Id = 34,
            Name = "Algorithm",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [
              "EddyCurrent",
            ],
            Steps = []
          },
            new()
          {
            Id = 35,
            Name = "Model",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 0,
            Default = "",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 36,
            Name = "Polish Height",
            Unit = "mm",
            Type = "float",
            Min = 0,
            Max = 20,
            Default = "3.00",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 37,
            Name = "Slurry Start Position",
            Unit = "deg",
            Type = "float",
            Min = 10,
            Max = 50,
            Default = "10.00",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 38,
            Name = "Slurry End  Position",
            Unit = "deg",
            Type = "float",
            Min = 10,
            Max = 50,
            Default = "10.00",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 39,
            Name = "Slurry Sweep Velocity",
            Unit = "deg/s",
            Type = "float",
            Min = 1,
            Max = 50,
            Default = "10.00",
            Options = [],
            Steps = []
          },
        ];
    }
}

