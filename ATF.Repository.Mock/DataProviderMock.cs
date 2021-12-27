﻿using Terrasoft.Common;
using Terrasoft.Core.DB;

namespace ATF.Repository.Mock
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ATF.Repository.Mock.Internal;
	using ATF.Repository.Providers;
	using Terrasoft.Nui.ServiceModel.DataContract;

	public class DataProviderMock: IDataProvider
	{
		private readonly Dictionary<string, DefaultValuesMock> _defaultValuesMocks;
		private readonly List<ItemsMock> _collectionItemsMocks;
		private readonly List<ScalarMock> _scalarItemsMocks;
		private readonly List<MockSavingItem> _batchItemMocks;


		public DataProviderMock() {
			_defaultValuesMocks = new Dictionary<string, DefaultValuesMock>();
			_collectionItemsMocks = new List<ItemsMock>();
			_scalarItemsMocks = new List<ScalarMock>();
			_batchItemMocks = new List<MockSavingItem>();
		}

		public IDefaultValuesMock MockDefaultValues(string schemaName) {
			if (!_defaultValuesMocks.ContainsKey(schemaName)) {
				_defaultValuesMocks.Add(schemaName, new DefaultValuesMock(schemaName));
			}
			return _defaultValuesMocks[schemaName];
		}

		public IDefaultValuesResponse GetDefaultValues(string schemaName) {
			if (!_defaultValuesMocks.ContainsKey(schemaName)) {
				return null;
			}
			var mock = _defaultValuesMocks[schemaName];
			mock.OnReceived();
			return mock.GetDefaultValues();
		}

		public IItemsMock MockItems(string schemaName) {
			var mock = new ItemsMock(schemaName);
			_collectionItemsMocks.Add(mock);
			return mock;
		}

		public IScalarMock MockScalar(string schemaName, AggregationScalarType aggregationType) {
			var mock = new ScalarMock(schemaName, aggregationType);
			_scalarItemsMocks.Add(mock);
			return mock;
		}

		public IItemsResponse GetItems(SelectQuery selectQuery) {
			var mock = GetScalarMock(selectQuery) ?? GetCollectionMock(selectQuery);
			if (mock == null) {
				return null;
			}
			mock.OnReceived();
			return mock.GetItemsResponse();
		}

		private BaseMock GetScalarMock(SelectQuery selectQuery) {
			if (selectQuery.Columns.Items.Count() != 1) {
				return null;
			}

			var column = selectQuery.Columns.Items.First();
			if (column.Value.Expression.AggregationType == AggregationType.None) {
				return null;
			}
			var queryParameters = QueryParametersExtractor.ExtractParameters(selectQuery);
			return _scalarItemsMocks.FirstOrDefault(x =>
				x.SchemaName == selectQuery.RootSchemaName && x.CheckByParameters(queryParameters));
		}

		private BaseMock GetCollectionMock(SelectQuery selectQuery) {
			var queryParameters = QueryParametersExtractor.ExtractParameters(selectQuery);
			return _collectionItemsMocks.FirstOrDefault(x =>
				x.SchemaName == selectQuery.RootSchemaName && x.CheckByParameters(queryParameters));
		}

		public IExecuteResponse BatchExecute(List<BaseQuery> queries) {
			queries.ForEach(ReceiveBatchQueryItem);
			return new ExecuteResponse() {
				Success = true,
				ErrorMessage = string.Empty,
				QueryResults = new List<IExecuteItemResponse>()
			};
		}

		public IMockSavingItem MockSavingItem(string entitySchema, SavingOperation operation) {
			var mock = new MockSavingItem(entitySchema, operation);
			_batchItemMocks.Add(mock);
			return mock;
		}

		private void ReceiveBatchQueryItem(BaseQuery query) {
			if (query is InsertQuery insertQuery) {
				ReceiveBatchQueryItem(insertQuery);
			} else if (query is UpdateQuery updateQuery) {
				ReceiveBatchQueryItem(updateQuery);
			} else if (query is DeleteQuery deleteQuery) {
				ReceiveBatchQueryItem(deleteQuery);
			} else {
				throw new NotSupportedException();
			}
		}

		private void ReceiveBatchQueryItem(InsertQuery query) {
			var columnValueParameters = QueryParametersExtractor.ExtractColumnValues(query);
			var mock = _batchItemMocks.FirstOrDefault(x =>
				x.SchemaName == query.RootSchemaName && x.Operation == SavingOperation.Insert &&
				x.CheckByColumnValues(columnValueParameters));
			mock?.OnReceived();
		}

		private void ReceiveBatchQueryItem(UpdateQuery query) {
			var columnValueParameters = QueryParametersExtractor.ExtractColumnValues(query);
			var queryParameters = QueryParametersExtractor.ExtractParameters(query);
			var mock = _batchItemMocks.FirstOrDefault(x =>
				x.SchemaName == query.RootSchemaName &&
				x.Operation == SavingOperation.Update && x.CheckByColumnValues(columnValueParameters) &&
				x.CheckByParameters(queryParameters));
			mock?.OnReceived();
		}

		private void ReceiveBatchQueryItem(DeleteQuery query) {
			var queryParameters = QueryParametersExtractor.ExtractParameters(query);
			var mock = _batchItemMocks.FirstOrDefault(x =>
				x.SchemaName == query.RootSchemaName && x.Operation == SavingOperation.Delete &&
				x.CheckByParameters(queryParameters));
			mock?.OnReceived();
		}
	}
}
