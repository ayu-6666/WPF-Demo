using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CPM.Recipe.Tool.Models;

namespace CPM.Recipe.Tool.Templates
{
    public static class ConditionRecipeTemplate
    {
        public static ObservableCollection<RecipeModel.Section> Template =
        [
            new()
          {
            Id = 1,
            Name = "Process Type",
            Unit = "",
            Type = "option",
            Min = 1,
            Max = 2,
            Default = "",
            Options = ["1:InsituCondition", "25:ExsituCondition", "-1:Recipe End"],
            Steps = []
          },
            new()
          {
            Id = 2,
            Name = "Process Time",
            Unit = "s",
            Type = "float",
            Min = 0,
            Max = 10000,
            Default = "100",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 3,
            Name = "InSitu",
            Unit = "",
            Type = "list",
            Min = -1,
            Max = 26,
            Default = "5",
            Options = ["1:On","2:Exsit"],
            Steps = []
          },
            new()
          {
            Id = 4,
            Name = "Platen Rotation",
            Unit = "r/min",
            Type = "float",
            Min = 300,
            Max = 3000,
            Default = "2",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 5,
            Name = "Condition Head Rotation",
            Unit = "r/min",
            Type = "float",
            Min = 300,
            Max = 3000,
            Default = "2",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 6,
            Name = "Condition Pressure",
            Unit = "N",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "2",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 7,
            Name = "Condition Sweep Recipe",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 5,
            Default = "",
            Options = [
              "0:None",
              "1:Condition Repice1",
              "2:Condition Repice2",
              "3:Condition Repice3",
              "4:Condition Repice4",
              "5:Condition Repice5"
            ],
            Steps = []
          },
            new()
          {
            Id = 8,
            Name = "Plant Wash",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "0",
            Options = ["0:Close", "1:Open"],
            Steps = []
          },
            new()
          {
            Id = 9,
            Name = "Slurry Line 1 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "",
            Options = ["0:Slurry", "1:DIW"],
            Steps = []
          },
            new()
          {
            Id = 10,
            Name = "Slurry Line 2 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "",
            Options = ["0:Slurry", "1:DIW"],
            Steps = []
          },
            new()
          {
            Id = 11,
            Name = "Slurry Line 3 Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "",
            Options = ["0:Slurry", "1:DIW"],
            Steps = []
          },
            new()
          {
            Id = 12,
            Name = "Slurry Line 1 Flow",
            Unit = "mL/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "2",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 13,
            Name = "Slurry Line 2 Flow",
            Unit = "mL/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "2",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 14,
            Name = "Slurry Line 3 Flow",
            Unit = "mL/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "2",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 15,
            Name = "Sweep Stop Time Type",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 1,
            Default = "0",
            Options = ["0:Time", "1:Ratio"],
            Steps = []
          },
             new()
          {
            Id = 16,
            Name = "Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "2",
            Options = [],
            Steps = []
          },
        ];
    }
}
