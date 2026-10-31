using CPM.Recipe.Tool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPM.Recipe.Tool.Templates
{
    public static class DryRecipeTemplate
    {
        public static ObservableCollection<RecipeModel.Section> Template =
        [
            new()
          {
            Id = 1,
            Name = "Process Type",
            Unit = "",
            Type = "option",
            Min = -1,
            Max = 3,
            Default =  "3: Dry",
            Options = ["1: Dry With Arm", "3: Dry", "0: Disabled", "-1: Recipe End"],
            Steps = []
          },
            new()
          {
            Id = 2,
            Name = "Process Time",
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
            Id = 3,
            Name = "Wafer Rotation Speed",
            Unit = "r/min",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 4,
            Name = "IPA Arm Sweep Start Position",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 6,
            Default = "1: Start Position",
            Options = ["1: Start Position","2: Near Center Position","3: Middle Position","4: Near Edge","5: Edge Position"],
            Steps = []
          },
            new()
          {
            Id = 5,
            Name = "IPA Arm Sweep End Position",
            Unit = "",
            Type = "list",
            Min = 0,
            Max = 6,
            Default = "1: Start Position",
            Options = ["1: Start Position","2: Near Center Position","3: Middle Position","4: Near Edge","5: Edge Position"],
            Steps = []
          },
            new()
          {
            Id = 6,
            Name = "IPA Arm Sweep Center Velocity",
            Unit = "deg/s",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 7,
            Name = "IPA Arm Sweep Edge Velocity",
            Unit = "deg/s",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 8,
            Name = "IPA Arm Sweep Speed",
            Unit = "deg/s",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 9,
            Name = "IPA Arm Cycle",
            Unit = "Times",
            Type = "int",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 10,
            Name = "Dilution N2 Gas Flow",
            Unit = "L/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 11,
            Name = "N2 For WF Backside Flow",
            Unit = "L/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 12,
            Name = "Outer Rinse Flow",
            Unit = "L/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 13,
            Name = "Bubbling N2 Gas Flow",
            Unit = "L/min",
            Type = "float",
            Min = 0,
            Max = 3000,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 14,
            Name = "Upper Rinse Supply",
            Unit = "",
            Type = "option",
            Min = 0,
            Max = 1,
            Default = "0:Disabled",
            Options = ["0:Disabled", "1:Enabled"],
            Steps = []
          },
             new()
          {
            Id = 15,
            Name = "Lower Rinse Supply",
            Unit = "",
            Type = "option",
            Min = 0,
            Max = 1,
            Default = "0:Disabled",
            Options = ["0:Disabled", "1:Enabled"],
            Steps = []
          },
             new()
          {
            Id = 16,
            Name = "Wafer Rotation Acceleration",
            Unit = "r/min2",
            Type = "float",
            Min = 0,
            Max = 1000,
            Default = "0",
            Options = [],
            Steps = []
          },
        ];
    }
}

