using CPM.Recipe.Tool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPM.Recipe.Tool.Templates
{
    public static class ExsituContidionSweepRecipeTemplate
    {
        public static ObservableCollection<RecipeModel.Section> Template =
        [
            new()
          {
            Id = 1,
            Name = "Condition Sweep Type",
            Unit = "",
            Type = "option",
            Min = 1,
            Max = 2,
            Default = "",
            Options = ["1:Fix", "2:Traverse"],
            Steps = []
          },
            new()
          {
            Id = 2,
            Name = "Condition Sweep Count",
            Unit = "count/min",
            Type = "int",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 3,
            Name = "Start Position",
            Unit = "deg",
            Type = "float",
            Min = 0,
            Max = 180,
            Default = "15",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 4,
            Name = "End Position",
            Unit = "deg",
            Type = "float",
            Min = 0,
            Max = 180,
            Default = "65",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 5,
            Name = "Zone Count",
            Unit = "",
            Type = "int",
            Min = 0,
            Max = 20,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 6,
            Name = "Zone 1 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 7,
            Name = "Zone 2 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 8,
            Name = "Zone 3 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 9,
            Name = "Zone 4 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 10,
            Name = "Zone 5 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 11,
            Name = "Zone 6 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 12,
            Name = "Zone 7 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 13,
            Name = "Zone 8 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 14,
            Name = "Zone 9 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 15,
            Name = "Zone 10 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 16,
            Name = "Zone 11 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 17,
            Name = "Zone 12 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 18,
            Name = "Zone 13 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 19,
            Name = "Zone 14 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 20,
            Name = "Zone 15 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 21,
            Name = "Zone 16 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 22,
            Name = "Zone 17 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 23,
            Name = "Zone 18 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
             new()
          {
            Id = 24,
            Name = "Zone 19 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
            new()
          {
            Id = 25,
            Name = "Zone 20 Ratio",
            Unit = "",
            Type = "float",
            Min = 0,
            Max = 100,
            Default = "0",
            Options = [],
            Steps = []
          },
        ];
    }
}

