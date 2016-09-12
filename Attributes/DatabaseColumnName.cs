using System;

namespace BAS.DataLayer.Attributes
{
  public class DatabaseColumnName : Attribute
  {
    public readonly string DbColumnName;

    public DatabaseColumnName(string databaseColumnName)
    {
      this.DbColumnName = databaseColumnName;
    }
  }
}