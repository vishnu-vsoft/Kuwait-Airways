using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PuppeteerSharp;
using System.Globalization;
using System.Text.Json;



public class AirlineScraping
{
   
    public async static Task Main(string[] args)
    {
        AirlineScraping airlineScraping = new AirlineScraping();
        string id = "229-16794304";
        await airlineScraping.SaudiCargo(id);
    }

    private TaskCompletionSource<string?>? _waitForResponse;
    string? desc; 
    string? formatedFlightNo;
    public async Task SaudiCargo(string id)
    {
        
        var launchOptions = new LaunchOptions()
        {
            Headless = false,
            ExecutablePath = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
        };
        _waitForResponse = new TaskCompletionSource<string?>();
        var browser = await Puppeteer.LaunchAsync(launchOptions);
        var page = await browser.NewPageAsync();
        await page.GoToAsync("https://www.kuwaitairways.com/en/cargo/tracking");

        var awb = id.Split("-");
       var prefix = await page.WaitForSelectorAsync("#txtPrefixNo");
        await prefix.TypeAsync(awb[0]);

        var second = await page.WaitForSelectorAsync("#txtAwbNo");
        await second.TypeAsync(awb[1]);

        page.Response += HandleResponseAsync;

        var submitButton = await page.WaitForSelectorAsync("#btnSubmit");
        await submitButton.ClickAsync();
        var completedTask = await Task.WhenAny(_waitForResponse.Task, Task.Delay(TimeSpan.FromSeconds(60)));

        string? jsonResponse = null;
        

        if (completedTask == _waitForResponse.Task)
        {
            jsonResponse = _waitForResponse.Task.Result;
        }
        else
        {
            var errMsg = "Timeout while waiting for response.";
            Console.WriteLine(errMsg);
        }



        //if invalid awb, return null




        Rootobject? root; 
        AWBStatus awbstatus = new AWBStatus();
        List<FlightDetail> flightDetails = new List<FlightDetail>();
        FlightDetail flight = new FlightDetail();
        
        if (jsonResponse != null)
        {
            root = JsonConvert.DeserializeObject<Rootobject>(jsonResponse);
            if (root != null) {
                foreach (var item in root.Routing)
                {
                    flight.Number = item.FlightNum.Insert(2, " ");

                    //var code = flightno.Insert(2, " ");
                    //var flightCode = code.Split(" ")[0];
                    bool date = DateTime.TryParse(item.FlightDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime x);
                    flight.Date = x.ToString("dd-MMM");

                    flight.Origin = item.FlightOrigin;

                    flight.Destination = item.FlightDest;

                    desc = item.StatusMessage;
                    flightDetails.Add(flight);

                }
                List<FlightSchedules> flightSchedule = new List<FlightSchedules>(); 
                

                foreach (var segmentItem in root.Segments) 
                {
                    FlightSchedules schedule = new FlightSchedules();
                    var flno = segmentItem.FlightNum;
                    if (flno == string.Empty)
                    {
                        schedule.FlightNo = flno;
                        schedule.AirlineCode = string.Empty;
                    }
                    else
                    {
                        string[] splitting = flno.Insert(2, "-").Split("-"); //check for length before insert. out f bout error. do this everywhere
                        var lastFlightNo = splitting.Last();
                        if (lastFlightNo.Length <= 3)
                        {
                            var insert0 = lastFlightNo.Insert(0, "0");
                            string? formatedFlightNo = (splitting.First() + insert0).Insert(2, "-");
                            schedule.FlightNo = formatedFlightNo;
                            schedule.AirlineCode = formatedFlightNo.Split("-")[0];
                        }
                    }
                    schedule.Station = segmentItem.FlightAirPort;

                    schedule.Destination = segmentItem.Dest;

                    string flightArrivalDate = segmentItem.ArrivalDate + " " + segmentItem.ArrivalTime;
                    if (string.IsNullOrWhiteSpace(flightArrivalDate))
                    {
                        schedule.ETA = string.Empty;
                    }
                    else {
                        bool arrivalDate = DateTime.TryParse(flightArrivalDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime arrivalDateTime);
                        schedule.ETA = arrivalDateTime.ToString("dd/MM/yyyy | HH:mm");
                    }

                    string flightDepartureDate = segmentItem.EventDate + " " + segmentItem.EventTime;
                    if (string.IsNullOrWhiteSpace(flightDepartureDate))
                    {
                        schedule.ETD = string.Empty;
                    }
                    else
                    {
                        bool arrivalDate = DateTime.TryParse(flightDepartureDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime departurelDateTime);
                        schedule.ETD = departurelDateTime.ToString("dd/MM/yyyy | HH:mm");
                    }
                    flightSchedule.Add(schedule);
                    awbstatus.FlightSchedules = flightSchedule;
                }






                List<StatusHistory> statusHistories = new List<StatusHistory>();

                int i = 0;
                
                foreach (var routingBoxItem in root.RoutingBox)
                {
                    StatusHistory status1 = new StatusHistory();
                    status1.OrderNumber = i;
                    status1.Station = routingBoxItem.FlightAirPort;
                    status1.MileStone = routingBoxItem.StatusCode1;
                    status1.Status = routingBoxItem.StatusCode2;
                    status1.Pcs = routingBoxItem.Pieces;
                    status1.ActualPieces = routingBoxItem.Pieces;
                    status1.Weight = routingBoxItem.Weight;
                    status1.FlightNo = flight.Number.Replace(" ", "");
                    status1.AirlineCode = status1.FlightNo.Insert(2, " ").Split(" ")[0];
                    awbstatus.Status = status1.Status;
                    status1.Origin = flight.Origin;
                    status1.Destination = flight.Destination;
                    status1.ULD = string.Empty;
                    string flightDateTime = routingBoxItem.EventDate1 + " " + routingBoxItem.EventTime1;
                    bool arrivalDate = DateTime.TryParse(flightDateTime, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateTime);
                    status1.EventDateTime = dateTime.ToString("dd/MM/yyyy HH:mm");
                    status1.FlightDate = status1.EventDateTime.Split(" ")[0];
                    i++;
                    statusHistories.Add(status1);
                    awbstatus.StatusHistories = statusHistories;
                }
            }
                awbstatus.LastActivity = root?.Origin ?? ""; //what if root is null? handle it
                awbstatus.LastActivityDate = root?.CurrentStatus ?? "";
                awbstatus.DONo = "";
                awbstatus.DownloadLink = "";
                awbstatus.Flights = flight.Number;
                awbstatus.AWB = root?.AwbNo ?? "";
                awbstatus.Pieces = root?.Pieces ?? "";
                
                awbstatus.Weight = root.Weight;
                awbstatus.FlightDetails = flightDetails;
                
                      
                awbstatus.ULDNo = "";
                awbstatus.HAWBNo = "";
                awbstatus.IssuedTo = "";
                awbstatus.Origin = root.Origin;
                awbstatus.Destination = root.Destination;
                awbstatus.LastActivityDescription = desc;
                string json = JsonConvert.SerializeObject(awbstatus,Formatting.Indented);
                Console.WriteLine(json);
            }   
        
    }
    

    public async void HandleResponseAsync(object? x, ResponseCreatedEventArgs e)
        {
            if (e.Response.Url.ToLower().Contains("api/GetCargoInfo".ToLower()))
            {
                var jsonBody = await e.Response.TextAsync();
                _waitForResponse.SetResult(jsonBody); //h
                
            }
        
    }

}



public class AWBTrackRequest
{
    public string AWBNo { get; set; } = string.Empty;
}
public class AWBStatus
{
    public string LastActivity { get; set; } = string.Empty;
    public string LastActivityDate { get; set; } = string.Empty;
    public string DONo { get; set; } = string.Empty;
    public string DownloadLink { get; set; } = string.Empty;
    public string AWB { get; set; } = string.Empty;
    public string Pieces { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Flights { get; set; } = string.Empty;
    public List<FlightDetail> FlightDetails { get; set; } = new List<FlightDetail>();
    public string ULDNo { get; set; } = string.Empty;
    public string HAWBNo { get; set; } = string.Empty;
    public string IssuedTo { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string LastActivityDescription { get; set; } = string.Empty;
    public List<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
    public List<FlightSchedules> FlightSchedules { get; set; } = new List<FlightSchedules>();
}

public class FlightSchedules
{
    public string FlightNo { get; set; } = string.Empty;
    public string AirlineCode { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string ETA { get; set; } = string.Empty;
    public string ETD { get; set; } = string.Empty;
}
public class StatusHistory
{
    public int OrderNumber { get; set; }
    public string Station { get; set; } = string.Empty;
    public string MileStone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Pcs { get; set; } = string.Empty;
    public string ActualPieces { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string FlightNo { get; set; } = string.Empty;
    public string AirlineCode { get; set; } = string.Empty;
    public string FlightDate { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string ULD { get; set; } = string.Empty;
    public string EventDateTime { get; set; } = string.Empty;
}
public class FlightDetail
{
    public string Number { get; set; } = "";
    public string Date { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
}



public class Rootobject
{
    public string AwbNo { get; set; }
    public string Origin { get; set; }
    public string Destination { get; set; }
    public string Pieces { get; set; }
    public string Weight { get; set; }
    public string Volume { get; set; }
    public string CurrentStatus { get; set; }
    public Routing[] Routing { get; set; }
    public Routingbox[] RoutingBox { get; set; }
    public Segment[] Segments { get; set; }
    public string ResponseText { get; set; }
    public string ResponseCode { get; set; }
}

public class Routing
{
    public string FlightOrigin { get; set; }
    public string FlightDest { get; set; }
    public string FlightNum { get; set; }
    public string FlightDate { get; set; }
    public string Alloc { get; set; }
    public string Status { get; set; }
    public string Pieces { get; set; }
    public string Weight { get; set; }
    public string Volume { get; set; }
    public string FlightCat { get; set; }
    public string StatusMessage { get; set; }
    public int SequenceNo { get; set; }
    public int isAllowed { get; set; }
    public object Shipment_Id { get; set; }
}

public class Routingbox
{
    public string StatusCode1 { get; set; }
    public string StatusCode2 { get; set; }
    public string FlightAirPort { get; set; }
    public string Pieces { get; set; }
    public string Weight { get; set; }
    public string EventDate1 { get; set; }
    public string EventTime1 { get; set; }
    public string EventDate2 { get; set; }
    public string EventTime2 { get; set; }
    public string StatusMessage1 { get; set; }
    public string StatusMessage2 { get; set; }
    public int SequenceNo1 { get; set; }
    public int SequenceNo2 { get; set; }
    public int cBoxNo { get; set; }
}

public class Segment
{
    public string Origin { get; set; }
    public string Dest { get; set; }
    public string FlightNum { get; set; }
    public string FlightCat { get; set; }
    public string FlightDate { get; set; }
    public string ArrivalDate { get; set; }
    public string ArrivalTime { get; set; }
    public string NumPieces { get; set; }
    public string Weight { get; set; }
    public string Volume { get; set; }
    public string Status { get; set; }
    public string EventDate { get; set; }
    public string EventTime { get; set; }
    public string StatusCode { get; set; }
    public int SequenceNo { get; set; }
    public string StatusMessage { get; set; }
    public string FlightAirPort { get; set; }
    public string StatusFullDetails { get; set; }
}


