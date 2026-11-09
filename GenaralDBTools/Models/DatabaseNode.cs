using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GenaralDBTools.Models;

public static class NodeTypes
{
    public const string Connection = "Connection";
    public const string Schema = "Schema";
    public const string Table = "Table";
    public const string View = "View";
    public const string Column = "Column";
    public const string RedisDatabase = "RedisDatabase";
    public const string RedisKey = "RedisKey";
    public const string MongoDatabase = "MongoDatabase";
    public const string Collection = "Collection";
    public const string Hint = "Hint";
}

public sealed class DatabaseNode : ObservableObject
{
    private string _name = "";
    private bool _isConnected;
    private bool _isLoading;
    private bool _hasLoadedChildren;
    private string? _loadError;
    private string _icon = "▫";

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string NodeType { get; set; } = "";
    public string? ProfileId { get; set; }
    public string? SchemaName { get; set; }
    public string? ObjectName { get; set; }
    public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public bool HasLoadedChildren { get => _hasLoadedChildren; set => SetProperty(ref _hasLoadedChildren, value); }
    public string? LoadError { get => _loadError; set => SetProperty(ref _loadError, value); }
    public string Icon { get => _icon; set => SetProperty(ref _icon, value); }
    public ObservableCollection<DatabaseNode> Children { get; set; } = new();

    public bool IsTableLike => NodeType is NodeTypes.Table or NodeTypes.View;
    public bool IsConnectable => NodeType == NodeTypes.Connection;
}
