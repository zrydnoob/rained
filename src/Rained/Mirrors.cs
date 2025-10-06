namespace Rained;

class MirrorManager
{
  private readonly List<MirrorSource> mirrorSources = new List<MirrorSource>();

  public int SelectedIndex { get; set; } = 0;

  public MirrorManager()
  {
    AddMirror("Github", "https://github.com/SlimeCubed/Drizzle.Data/archive/refs/heads/community.zip");
    AddMirror("llkk.cc", "https://gh.llkk.cc/https://github.com/SlimeCubed/Drizzle.Data/archive/refs/heads/community.zip");
    AddMirror("ghproxy.net", "https://ghproxy.net/https://github.com/SlimeCubed/Drizzle.Data/archive/refs/heads/community.zip");
    AddMirror("bgithub.xyz", "https://bgithub.xyz/SlimeCubed/Drizzle.Data/archive/refs/heads/community.zip");
  }

  private string[] GetAllMirrorNames()
  {
    return mirrorSources.Select(m => m.Name).ToArray();
  }

  public String MirrorComboItems()
  {
    var mirrorNames = GetAllMirrorNames();
    string comboItems = string.Join("\0", mirrorNames) + "\0";

    return comboItems;
  }

  public string GetSelectedUrl()
  {
    if (SelectedIndex >= 0 && SelectedIndex < mirrorSources.Count)
    {
      return mirrorSources[SelectedIndex].Url;
    }
    return string.Empty;
  }

  public void AddMirror(String name, String url)
  {
    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
    {
      mirrorSources.Add(new MirrorSource(name, url));
    }
  }

  private class MirrorSource
  {
    public String Name;
    public String Url;

    public MirrorSource(String name, String url)
    {
      Name = name;
      Url = url;
    }
  }
}