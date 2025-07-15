# Internal Certificates

## References

- https://www.redhat.com/en/blog/configure-ca-trust-list
- https://arminreiter.com/2022/01/create-your-own-certificate-authority-ca-using-openssl/
- https://hohnstaedt.de/xca/index.php/software/changelog

## Setup

First setup a CA if you don't have one already and mark it as trusted in the system:

- Use XCA to generate CA
- export CA to PEM (do not include key)
- [as root]
  - change file ownership to root:root
  - copy to either:
    - /usr/share/pki/ca-trust-source/anchors
    - /etc/pki/ca-trust/source/anchors
  - update-ca-trust

Next create a certificate to be used by our internal sites that our browsers can trust:

- Use XCA to generate new server certificate
- export cert and key to PFX file and put in the appropriate site for the application and note the password
- configure the application/environment w/ the path and password so kestrel can load it

Finally, if necessary, update /etc/hosts with a new alias to point at the site/dev site as needed.

## Notes

HTTP Redirects not used as apis typically won't follow this as it isn't a traditional user-agent / browser.

Restart Chrome to make sure it sees the new certs.  You can also confirm that Chrome recognizes the new CA by
going to Settings > Privacy and security > Security > Manage certificates
