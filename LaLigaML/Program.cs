using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Database;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace LaLigaML
{
    class Program
    {
        //https://github.com/dotnet/machinelearning-samples/blob/main/samples/csharp/getting-started/Forecasting_BikeSharingDemand/BikeDemandForecasting/Program.cs
        //https://docs.microsoft.com/en-us/dotnet/machine-learning/tutorials/time-series-demand-forecasting

        static void Main(string[] args)
        {
            var connectionString = $"Data Source=.;Initial Catalog=LaLiga;Integrated Security=SSPI;";

            CreateAndLoadDatabase();

            var mlContext = new MLContext();
            var loader = mlContext.Data.CreateDatabaseLoader<Match>();
            string query = "SELECT [Date],[HomeTeamName],[AwayTeamName],[HomeTeamGoals],[AwayTeamGoals],[Result] FROM Rentals";

            DatabaseSource dbSource = new DatabaseSource(SqlClientFactory.Instance, connectionString, query);

            var dataView = loader.Load(dbSource);


            IDataView firstYearData = mlContext.Data.FilterRowsByColumn(dataView, "Year", upperBound: 1);
            IDataView secondYearData = mlContext.Data.FilterRowsByColumn(dataView, "Year", lowerBound: 1);

            var forecastingPipeline = mlContext.Forecasting.ForecastBySsa(
                outputColumnName: "ForecastedRentals",
                inputColumnName: "TotalRentals",
                windowSize: 7,
                seriesLength: 30,
                trainSize: 365,
                horizon: 7,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: "LowerBoundRentals",
                confidenceUpperBoundColumn: "UpperBoundRentals");

            SsaForecastingTransformer forecaster = forecastingPipeline.Fit(firstYearData);

            Evaluate(secondYearData, forecaster, mlContext);

            var forecastEngine = forecaster.CreateTimeSeriesEngine<ModelInput, ModelOutput>(mlContext);
            forecastEngine.CheckPoint(mlContext, modelPath);

            Forecast(secondYearData, 7, forecastEngine, mlContext);

            void Evaluate(IDataView testData, ITransformer model, MLContext mlContext)
            {
                // Make predictions
                IDataView predictions = model.Transform(testData);

                // Actual values
                IEnumerable<float> actual =
                    mlContext.Data.CreateEnumerable<ModelInput>(testData, true)
                        .Select(observed => observed.TotalRentals);

                // Predicted values
                IEnumerable<float> forecast =
                    mlContext.Data.CreateEnumerable<ModelOutput>(predictions, true)
                        .Select(prediction => prediction.ForecastedRentals[0]);

                // Calculate error (actual - forecast)
                var metrics = actual.Zip(forecast, (actualValue, forecastValue) => actualValue - forecastValue);

                // Get metric averages
                var MAE = metrics.Average(error => Math.Abs(error)); // Mean Absolute Error
                var RMSE = Math.Sqrt(metrics.Average(error => Math.Pow(error, 2))); // Root Mean Squared Error

                // Output metrics
                Console.WriteLine("Evaluation Metrics");
                Console.WriteLine("---------------------");
                Console.WriteLine($"Mean Absolute Error: {MAE:F3}");
                Console.WriteLine($"Root Mean Squared Error: {RMSE:F3}\n");
            }

            void Forecast(IDataView testData, int horizon, TimeSeriesPredictionEngine<ModelInput, ModelOutput> forecaster, MLContext mlContext)
            {

                ModelOutput forecast = forecaster.Predict();

                IEnumerable<string> forecastOutput =
                    mlContext.Data.CreateEnumerable<ModelInput>(testData, reuseRowObject: false)
                        .Take(horizon)
                        .Select((ModelInput rental, int index) =>
                        {
                            string rentalDate = rental.RentalDate.ToShortDateString();
                            float actualRentals = rental.TotalRentals;
                            float lowerEstimate = Math.Max(0, forecast.LowerBoundRentals[index]);
                            float estimate = forecast.ForecastedRentals[index];
                            float upperEstimate = forecast.UpperBoundRentals[index];
                            return $"Date: {rentalDate}\n" +
                            $"Actual Rentals: {actualRentals}\n" +
                            $"Lower Estimate: {lowerEstimate}\n" +
                            $"Forecast: {estimate}\n" +
                            $"Upper Estimate: {upperEstimate}\n";
                        });

                // Output predictions
                Console.WriteLine("Rental Forecast");
                Console.WriteLine("---------------------");
                foreach (var prediction in forecastOutput)
                {
                    Console.WriteLine(prediction);
                }
            }

        }

        private static void CreateAndLoadDatabase()
        {
            using var db = new LaLigaContext();
            db.Database.EnsureCreated();

            if (!db.Matches.Any())
            {
                FillDatabaseFromCsv(db);
            }
        }

        private static void FillDatabaseFromCsv(LaLigaContext db)
        {
            var csv = File.ReadAllText("matches.csv");
            var csvRows = csv.Split('\n');

            foreach (var row in csvRows)
            {
                if (!string.IsNullOrEmpty(row))
                {
                    var cells = row.Split(',');
                    var match = new Match()
                    {
                        Date = DateTime.ParseExact(cells[1], "d/m/yyyy", CultureInfo.InvariantCulture),
                        HomeTeamName = cells[2],
                        AwayTeamName = cells[3],
                        HomeTeamGoals = int.Parse(cells[4]),
                        AwayTeamGoals = int.Parse(cells[5])

                    };
                    db.Matches.Add(match);
                }
            }

            db.SaveChanges();
        }
    }
}
