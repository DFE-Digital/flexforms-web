# Changelog

All notable changes to this service will be documented in this file.

## [2.0.0]
### Notes
- FlexForms (Forms Engine SaaS)

---------------------------------------------
## [1.0.0]
### Notes
- First formally versioned public beta release.

## [1.0.1]
### Notes
- Added Client Side AppInsights SDK.

## [1.1.0]
### Notes
- Added Support for Multi-Tenancy. This service is now a Tenant of EAT API.

## [1.2.0]
### Notes
- As part of the Multi-Tenancy, we have now converted EAT Web to a single repository deployed to multiple services
- Each service will have it's own set of appsettings.json files, and it is decided at the time of the deployment which one is deployed to the container.

## [1.2.1]
### Notes
- Updated LSRP Test env appsettings with an update DSI auth details and Front-Door URL.

## [1.2.2]
### Notes
- Improved Event-Mapping to support multi handlers when an application is submitted.

## [1.2.3]
### Notes
- Added postcode to the Academies Auto-Complete confirmation page.
- Fixed a bug in Auto-Complete where duplicate items couldn't be selected.

## [1.2.4]
### Notes
- Enabled Test Auth in LSRP Test Environment.

## [1.2.5]
### Notes
- Updated Collection Flows to save user changes on each click of Save and Continue

## [1.2.6]
### Notes
- Fixed Collection Flow validation and conditional logic issues

## [1.2.8]
### Notes
- Added a flag to the template's Task node to support moving of the Task Summary to the end of the Task journey.

## [1.2.9]
### Notes
- Added support for checkboxes field

## [1.2.10]
### Notes
- Added support custom application names per tenant.

## [1.2.11]
### Notes
- Added max-words property to character count field.

## [1.2.12]
### Notes
- Added root level template property "hideFieldLabelWhenOnlyOneField" to toggle labels hiding for single field pages.

## [1.2.13]
### Notes
- Replaced hardcoded values with Layout:ServiceName

## [1.3.0]
### Notes
- Upgraded to .NET10

## [1.3.1]
### Notes
- Fixed issue with backlink not always returning to task summary

## [1.3.2]
### Notes
- Fixed wrong message showing when a new item has been added to a collection

## [1.3.3]
### Notes
- Reduced complexity of collection item added and updated messages

## [1.3.4]
### Notes
- Updated appsettings to include LSRP Prod environment details

## [1.3.5]
### Notes
- Fixed issue with CSS on flow descriptions

## [1.3.6]
### Notes
- Fixed issue with derived collection not being saved when edited

## [1.3.7]
### Notes
- Increased request size for Json template saving logic.

## [1.3.8]
### Notes
- Added support for EntraSSO authentication scheme.

## [1.3.9]
### Notes
- Fixed character count not counting characters properly on validation.

## [1.3.10]
### Notes
- Set Entra SSO enabled to false

## [1.3.11]
### Notes
- Added site-wide notification banner and feature flag

## [1.3.12]
### Notes
- Make email address configurable on invite a contributor page

## [1.3.13]
### Notes
- Fix Vision page executive summary word count bug

## [1.3.14]
### Notes
- Remove the word "form" from the plan version label

## [1.3.15]
### Notes
- Make the lead applicant label configurable in form header

## [1.3.16]
### Notes
- Added SignedOutCallBackUri to the DSI config

## [1.3.17]
### Notes
- Use a feature flag to disable submitting an application

## [1.3.18]
### Notes
- Added pagination to the dashboard

## [1.3.19]
### Notes
- Added Test env appsettings
- Updated pagination default page size to show 50 applications per page

## [1.3.20]
### Notes
- Added Test env appsettings

## [1.3.21]
### Notes
- Improved Logout functionality
- Improved caching and API error handling

## [1.3.22]
### Notes
- Allow contributor pattern to be disabled from the Template

## [1.3.23]
### Notes
- Added Prod env appsettings

## [1.3.24]
### Notes
- Improved caching

## [1.3.25]
### Notes
- Added LA and Diocese ComplexFields and details to the Auto-Complete search results and confirmation page.

## [1.3.26]
### Notes
- Added Academy filtering feature flag on the endpoint

## [1.3.27]
### Notes
- Added application search functionality

## [1.3.28]
### Notes
- Updated RGVisits service name

## [1.3.29]
### Notes
- Created read-only dashboard for listing all applications

## [1.3.30]
### Notes
- UCD changes for disabling the submit buuton

## [1.4.0]
### Notes
- Added feature for overriding the application statuses

## [1.4.1]
### Notes
- Accessibility issue with override custom application status form fixed