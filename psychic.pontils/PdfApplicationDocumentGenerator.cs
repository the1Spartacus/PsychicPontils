using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext _dataContext;
		private IPathProvider _templatePathProvider;
		public IViewGenerator _viewGenerator;
		internal readonly IConfiguration _configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
		private readonly IPdfGenerator _pdfGenerator;


		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			if (dataContext != null)
				throw new ArgumentNullException(nameof(dataContext));
			
			_dataContext = dataContext;
			_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException("templatePathProvider");
			_viewGenerator = viewGenerator;
			_configuration = configuration;
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_pdfGenerator = pdfGenerator;
		}
		
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			Application application = _dataContext.Applications.Single(app => app.Id == applicationId);

			var view = string.Empty;

            var pdfOptions = new PdfOptions
            {
                PageNumbers = PageNumbers.Numeric,
                HeaderOptions = new HeaderOptions
                {
                    HeaderRepeat = HeaderRepeat.FirstPageOnly,
                    HeaderHtml = PdfConstants.Header
                }
            };

            if (application != null)
			{

				if (baseUri.EndsWith("/"))
					baseUri = baseUri.Substring(baseUri.Length - 1);

				
				switch (application.State)
                {
					case ApplicationState.Pending:
						view = ApplicationStatePending(application, baseUri);
						break;
					case ApplicationState.Activated:
						view = ApplicationStateActivated(application, baseUri);
						break;
					case ApplicationState.InReview:
						view = ApplicationStateInReview(application, baseUri);
						break;
					default:
						_logger.LogWarning(
						$"The application is in state '{application.State}' and no valid document can be generated for it.");
						return null;
				}

	
				var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
				return pdf.ToBytes();
			}
			else
			{
				
				_logger.LogWarning(
					$"No application found for id '{applicationId}'");
				return null;
			}
		}

		public string ApplicationStatePending(Application application, string baseUri)
        {
			var path = _templatePathProvider.Get("PendingApplication");
			PendingApplicationViewModel vm = new PendingApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = application.Person.FirstName + " " + application.Person.Surname,
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature
			};
			 var view = _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), vm);
			return view;
		}
		public string ApplicationStateActivated(Application application, string baseUri) 
        {
			var path = _templatePathProvider.Get("ActivatedApplication");
			ActivatedApplicationViewModel vm = new ActivatedApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = $"{application.Person.FirstName} {application.Person.Surname}",
				LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products.SelectMany(p => p.Funds),
				PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
												.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
												.Sum(),
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature
			};
			var view = _viewGenerator.GenerateFromPath(baseUri + path, vm);
			return view;
		}
		public string ApplicationStateInReview(Application application, string baseUri)
        {
			var templatePath = _templatePathProvider.Get("InReviewApplication");
			var inReviewMessage = "Your application has been placed in review" +
								application.CurrentReview.Reason switch
								{
									{ } reason when reason.Contains("address") =>
										" pending outstanding address verification for FICA purposes.",
									{ } reason when reason.Contains("bank") =>
										" pending outstanding bank account verification.",
									_ =>
										" because of suspicious account behaviour. Please contact support ASAP."
								};
			var inReviewApplicationViewModel = new InReviewApplicationViewModel();
			inReviewApplicationViewModel.ReferenceNumber = application.ReferenceNumber;
			inReviewApplicationViewModel.State = application.State.ToDescription();
			inReviewApplicationViewModel.FullName = string.Format(
				"{0} {1}",
				application.Person.FirstName,
				application.Person.Surname);
			inReviewApplicationViewModel.LegalEntity =
				application.IsLegalEntity ? application.LegalEntity : null;
			inReviewApplicationViewModel.PortfolioFunds = application.Products.SelectMany(p => p.Funds);
			inReviewApplicationViewModel.PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
				.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
				.Sum();
			inReviewApplicationViewModel.InReviewMessage = inReviewMessage;
			inReviewApplicationViewModel.InReviewInformation = application.CurrentReview;
			inReviewApplicationViewModel.AppliedOn = application.Date;
			inReviewApplicationViewModel.SupportEmail = _configuration.SupportEmail;
			inReviewApplicationViewModel.Signature = _configuration.Signature;
			var view = _viewGenerator.GenerateFromPath($"{baseUri}{templatePath}", inReviewApplicationViewModel);
			return view;
		}
	}
}
