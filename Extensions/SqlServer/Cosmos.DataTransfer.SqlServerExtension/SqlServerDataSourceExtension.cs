﻿using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class SqlServerDataSourceExtension : IDataSourceExtension
    {
        public string DisplayName => "SqlServer";

        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<SqlServerSourceSettings>();
            settings.Validate();

            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using SqlCommand command = new SqlCommand(settings.QueryText, connection);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var columns = await reader.GetColumnSchemaAsync(cancellationToken);
                Dictionary<string, object?> fields = new Dictionary<string, object?>();
                foreach (var column in columns)
                {
                    var value = column.ColumnOrdinal.HasValue ? reader[column.ColumnOrdinal.Value] : reader[column.ColumnName];
                    if (value == DBNull.Value)
                    {
                        value = null;
                    }
                    fields[column.ColumnName] = value;
                }
                yield return new DictionaryDataItem(fields);
            }
        }
    }
}