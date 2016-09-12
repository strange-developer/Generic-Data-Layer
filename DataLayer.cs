using BAS.DataLayer.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace BAS.DataLayer
{
  public class DataLayer
  {
    private string _connectionString;
    private const string _defaultConnectionstringName = "ClarityConnection";
    private const string _internalTypeLibName = "mscorlib";

    public DataLayer()
    {
      _connectionString = ConfigurationManager.ConnectionStrings[_defaultConnectionstringName].ToString();
    }

    public DataLayer(string connectionStringName)
    {
      _connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ToString();
    }

    public bool ExecuteSpNoReturn(string procName, List<Parameter> parameterList = null)
    {
      int rowsAffected = 0;

      using (SqlConnection conn = new SqlConnection(_connectionString))
      {
        using (SqlCommand cmd = new SqlCommand(procName, conn))
        {
          cmd.CommandType = System.Data.CommandType.StoredProcedure;

          //add parameters if they exist
          if (parameterList != null)
          {
            foreach (Parameter param in parameterList)
            {
              cmd.Parameters.Add(param);
            }
          }
          conn.Open();
          rowsAffected = cmd.ExecuteNonQuery();
        }
      }

      return (rowsAffected != 0);
    }

    public T ExecuteSpReturnList<T>(string procName, List<Parameter> parameterList = null) where T : IList
    {
      T returnVal = default(T);
      using (SqlConnection conn = new SqlConnection(_connectionString))
      {
        using (SqlCommand cmd = new SqlCommand(procName, conn))
        {
          cmd.CommandType = System.Data.CommandType.StoredProcedure;

          //add parameters if they exist
          if (parameterList != null)
          {
            foreach (Parameter param in parameterList)
            {
              cmd.Parameters.Add(param);
            }
          }
          conn.Open();
          using (SqlDataReader dr = cmd.ExecuteReader())
          {
            if (dr.HasRows)
            {
              returnVal = PopulateReader_List<T>(dr);
            }
          }
        }
      }
      return returnVal;
    }

    public T ExecuteSpWithReturn<T>(string procName, List<Parameter> parameterList = null)
    {
      T returnVal = default(T);
      using (SqlConnection conn = new SqlConnection(_connectionString))
      {
        using (SqlCommand cmd = new SqlCommand(procName, conn))
        {
          cmd.CommandType = System.Data.CommandType.StoredProcedure;

          //add parameters if they exist
          if (parameterList != null)
          {
            foreach (Parameter param in parameterList)
            {
              cmd.Parameters.Add(param);
            }
          }
          conn.Open();
          using (SqlDataReader dr = cmd.ExecuteReader())
          {
            if (dr.HasRows)
            {
              if (!IsEnumerableType<T>(returnVal))
              {
                returnVal = PopulateReader_SingleObject<T>(dr);
              }
            }
          }
        }
      }
      return returnVal;
    }

    private bool IsEnumerableType<T>(T value)
    {
      bool isEnumerable = false;
      if (typeof(T).IsGenericType)
      {
        isEnumerable = typeof(T).GetInterfaces().Any(t => t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
      }
      return isEnumerable;
    }

    private T PopulateReader_List<T>(SqlDataReader dr) where T : IList
    {
      //get the generic type passed to the enumerable type
      Type innerType = typeof(T).GenericTypeArguments[0];
      PropertyInfo[] propertyList = null;
      if (IsUserDefinedType(innerType))
      {
        propertyList = innerType.GetProperties();
      }

      T valueList = Activator.CreateInstance<T>();

      while (dr.Read())
      {
        var value = innerType.GetType() == typeof(string) ? "" : Activator.CreateInstance(innerType);

        if (propertyList != null && propertyList.Length > 0)
        {
          SetPropertyValues(propertyList, dr, value, innerType);
          valueList.Add(value);
        }
        else
        {
          //we can determine here that we should only have one type in the object
          if (dr.FieldCount > 0)
          {
            //if the property list has no elements, we can safely assume that only a single property is being retrieved
            value = Convert.ChangeType(dr[0], innerType);
            valueList.Add(value);
          }
        }
      }
      return valueList;
    }

    private void SetPropertyValues(PropertyInfo[] propertyList, SqlDataReader dr, object value, Type type)
    {
      try
      {
        foreach (PropertyInfo propInfo in propertyList)
        {
          string propertyName = GetDatabaseColumnName(propInfo);
          string propValue = dr[propertyName].ToString();
          propInfo.SetValue(value, Convert.ChangeType(propValue, propInfo.PropertyType), null);
        }
      }
      catch (Exception e)
      {
        string s = ""; //Logging to be done
      }
    }

    private string GetDatabaseColumnName(PropertyInfo propInfo)
    {
      string dbColumnName = propInfo.Name;
      //check if a custom attribute is set in order to allow the caller to override the property name when retrieving results from the SQL DB.
      Attribute dbColumnNameAttrib = propInfo.GetCustomAttribute(typeof(DatabaseColumnName), false);
      if (dbColumnNameAttrib != null)
      {
        //only perform the typecast if the property was decorated with the DatabaseColumnName attribute
        dbColumnName = ((DatabaseColumnName)dbColumnNameAttrib).DbColumnName;
      }
      return dbColumnName;
    }

    private T CreateInstance<T>(Type type = null)
    {
      T instance = default(T);
      //check if type is string since string does not implement the iconvertible interface
      if (type != typeof(string) && type != null)
      {
        instance = Activator.CreateInstance<T>();
      }
      return instance;
    }

    //check if type is user defined
    private bool IsUserDefinedType(Type type)
    {
      bool isUserDefined = true;

      if (type.Assembly.GetName().Name == _internalTypeLibName)
      {
        isUserDefined = false;
      }

      return isUserDefined;
    }

    private T PopulateReader_SingleObject<T>(SqlDataReader dr)
    {
      Type genericType = typeof(T);
      T returnVal = CreateInstance<T>(genericType);
      PropertyInfo[] propertyList = null;
      if (IsUserDefinedType(genericType))
      {
        propertyList = genericType.GetProperties();
      }

      while (dr.Read())
      {
        if (propertyList != null && propertyList.Length > 0)
        {
          SetPropertyValues(propertyList, dr, returnVal, genericType);
        }
        else
        {
          //we can determine here that we should only have one type in the object
          if (dr.FieldCount > 0)
          {
            returnVal = ConvertType<T>(dr[0]);
          }
        }
      }

      return returnVal;
    }

    private T ConvertType<T>(object value)
    {
      return (T)Convert.ChangeType(value, typeof(T));
    }
  }

  public class Parameter
  {
    public string DatabaseParameterName { get; set; }
    public SqlDbType DataType { get; set; }
    public object DatabaseParameterValue { get; set; }

    public Parameter(string databaseParameterName, SqlDbType dbType, object databaseParameterValue)
    {
      DatabaseParameterName = databaseParameterName;
      DataType = dbType;
      DatabaseParameterValue = databaseParameterValue;
    }
  }
}