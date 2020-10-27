﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvertApi.Models;
using AdvertApi.Models.Messages;
using AdvertApi.Services;
using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace AdvertApi.Controllers
{
    [Route("adverts/v1")]
    [ApiController]
    public class Advert : ControllerBase
    {
        private readonly IAdvertStorageService _advertStorageService;
        private readonly IConfiguration _configuration;

        public Advert(IAdvertStorageService advertStorageService, IConfiguration configuration)
        {
            _advertStorageService = advertStorageService;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("Create")]
        [ProducesResponseType(404)]
        [ProducesResponseType(201, Type = typeof(AdvertResponse))]
        public async Task<IActionResult> Create(AdvertModel model)
        {
            string recordId;
            try
            {
                 recordId = await _advertStorageService.Add(model);
            }            
            catch(Exception exception)
            {
                return StatusCode(500, exception.Message);
                throw;
            }

            return StatusCode(201, new AdvertResponse { Id = recordId });
        }

        [HttpPut]
        [Route("Confirm")]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Confirm(ConfirmAdvertModel model)
        {
            try
            {
                await _advertStorageService.Confirm(model);

                //Raise the message to SNS
                await RaiseAdvertConfirmedMessage(model);
            }
            catch (KeyNotFoundException)
            {
                return new NotFoundResult();
            }
            catch (Exception exception)
            {
                return StatusCode(500, exception.Message);                
            }

            return new OkResult();
        }

        private async Task RaiseAdvertConfirmedMessage(ConfirmAdvertModel model)
        {
            var topicArn = _configuration.GetValue<string>("TopicArn");

            //Get title from DynanoMD
            var dbModel = await _advertStorageService.FindById(model.Id);
            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                var message = new AdvertConfirmedMessage
                {
                    Id = model.Id,
                    Title = dbModel.Title
                };

                var messageJson = JsonConvert.SerializeObject(message);
                await client.PublishAsync(topicArn, messageJson);
            }
        }
    }
}
