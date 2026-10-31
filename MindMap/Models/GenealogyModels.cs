using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MindMapWpf48.Models
{
    /// <summary>族谱成员</summary>
    [Serializable]
    public class Person
    {
        public Guid Id { get; set; }
        /// <summary>姓名</summary>
        public string Name { get; set; }
        /// <summary>性别："男" / "女"</summary>
        public string Gender { get; set; }
        /// <summary>出生</summary>
        public string Birth { get; set; }
        /// <summary>逝世</summary>
        public string Death { get; set; }
        /// <summary>字/号</summary>
        public string Courtesy { get; set; }
        /// <summary>备注</summary>
        public string Note { get; set; }
        /// <summary>无后（止）</summary>
        public bool LineEnded { get; set; }
        /// <summary>入继/过继（入）</summary>
        public bool Adopted { get; set; }
        /// <summary>妻子列表</summary>
        [XmlArray]
        public List<Person> Wives { get; set; }
        /// <summary>丈夫列表（女性成员的配偶）</summary>
        [XmlArray]
        public List<Person> Husbands { get; set; }
        /// <summary>子女列表（长幼顺序）</summary>
        [XmlArray]
        public List<Person> Children { get; set; }

        public Person()
        {
            Id = Guid.NewGuid();
            Name = "";
            Gender = "男";
            Birth = "";
            Death = "";
            Courtesy = "";
            Note = "";
            Wives = new List<Person>();
            Husbands = new List<Person>();
            Children = new List<Person>();
        }
    }

    /// <summary>族谱（多始祖支持，通常单根）</summary>
    [Serializable]
    [XmlRoot("Genealogy")]
    public class GenealogyModel
    {
        [XmlArray]
        public List<Person> Roots { get; set; }

        public GenealogyModel()
        {
            Roots = new List<Person>();
        }
    }
}
