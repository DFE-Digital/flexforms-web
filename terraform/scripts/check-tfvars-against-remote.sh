#!/bin/bash

# exit on failures
set -e
set -o pipefail

usage() {
  echo "Usage: $(basename "$0") [OPTIONS]" 1>&2
  echo "  -h                        - help"
  echo "  -a <storage_account>      - Azure Storage Account name"
  echo "  -c <storage_container>    - Azure Storage Container name"
  echo "  -f <tfvars_filename>      - Name of the tfvars file with file extension"
  exit 1
}

# if there are not arguments passed exit with usage
if [ $# -eq 0 ]
then
  usage
fi

while getopts "a:c:f:h" opt; do
  case $opt in
    a)
      STORAGE_ACCOUNT_NAME=$OPTARG
      ;;
    c)
      STORAGE_CONTAINER_NAME=$OPTARG
      ;;
    f)
      TFVARS_FILE_NAME=$OPTARG
      ;;
    h)
      usage
      ;;
    *)
      usage
      ;;
  esac
done

if [[
  -z "$STORAGE_ACCOUNT_NAME" ||
  -z "$STORAGE_CONTAINER_NAME" ||
  -z "$TFVARS_FILE_NAME"
]]
then
  usage
fi

set +e
STORAGE_CHECK=$(az storage blob list --account-name "$STORAGE_ACCOUNT_NAME" --container-name "$STORAGE_CONTAINER_NAME" 2>&1)
set -e

if ! jq -r >/dev/null 2>&1 <<< "$STORAGE_CHECK"
then
  exit 0
fi

LAST_UPDATED=$(jq -r \
  --arg name "$TFVARS_FILE_NAME" \
  '.[] | select(.name==$name) | .properties.lastModified' \
  <<< "$STORAGE_CHECK")

if [ -z "$LAST_UPDATED" ]
then
  exit 0
fi

LAST_UPDATED=$(echo "$LAST_UPDATED" | cut -d'+' -f1)
LAST_UPDATED_SECONDS=$(date -j -f "%Y-%m-%dT%H:%M:%S" "$LAST_UPDATED" "+%s")

if [ "$LAST_UPDATED_SECONDS" -gt "$(date -r "$TFVARS_FILE_NAME" +%s)" ]
then
  echo ""
  echo ""
  echo "Error: Your local tfvars file is older than the remote!"
  echo ""
  echo "Ensure you have the latest tfvars by running:"
  echo ""
  echo "  mv $TFVARS_FILE_NAME $TFVARS_FILE_NAME.old"
  echo "  az storage blob download \\"
  echo "    --file $TFVARS_FILE_NAME \\"
  echo "    --container-name $STORAGE_CONTAINER_NAME \\"
  echo "    --account-name $STORAGE_ACCOUNT_NAME \\"
  echo "    --name $TFVARS_FILE_NAME"
  echo ""
  echo "Or if you are sure your local tfvars are correct, just update the modified time by running:"
  echo ""
  echo "  touch $TFVARS_FILE_NAME"
  echo ""
  exit 1
fi
